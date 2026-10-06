#!/usr/bin/env bash
# File-based pre-release checks for a CrazyGames WebGL build of this template.
# Usage: bash .claude/skills/crazygames-release/scripts/preflight.sh   (from anywhere in the project)
# Output: one [PASS]/[FAIL]/[WARN]/[INFO] line per finding, then a summary.
# Exit codes: 0 = no FAIL, 1 = at least one FAIL, 2 = not run inside the project.

set -u

# ---------- Paths checked (relative to the project root) ----------
BUILD_SETTINGS="ProjectSettings/EditorBuildSettings.asset"
PLATFORM_CONFIG="Assets/_Project/ScriptableObjects/Config/PlatformConfig.asset"
PLATFORM_ENUM="Assets/_Core/Platform/Core/PlatformType.cs"
CRAZY_SETTINGS="Assets/CrazySDK/Resources/CrazyGamesSettings.asset"
PACKAGE_MANIFEST="Packages/manifest.json"
TEST_HARNESS_DIRS=(Assets/_Project/Testing Assets/_Platforms/CrazyGames/Testing)
GAME_CODE_DIRS=(Assets/_Core Assets/_Project Assets/_Platforms)
PLATFORM_LAYER_REGEX="^(Assets/_Core/Platform|Assets/_Platforms)/"
BOOTSTRAP_SCENE_NAME="00_Bootstrap"
TARGET_PLATFORM="CrazyGames"
# Packages that only run inside the Editor and never ship in a player build.
EDITOR_ONLY_PACKAGES='^com\.unity\.(ide\.|collab-proxy|test-framework|multiplayer\.center|2d\.aseprite|2d\.psdimporter|2d\.tooling|modules\.)'

FAILS=0; WARNS=0
pass() { echo "[PASS] $*"; }
fail() { echo "[FAIL] $*"; FAILS=$((FAILS + 1)); }
warn() { echo "[WARN] $*"; WARNS=$((WARNS + 1)); }
info() { echo "[INFO] $*"; }

# ---------- Locate the project root ----------
root="$(pwd)"
while [ "$root" != "/" ] && [ ! -f "$root/ProjectSettings/ProjectVersion.txt" ]; do root="$(dirname "$root")"; done
if [ ! -f "$root/ProjectSettings/ProjectVersion.txt" ]; then
    echo "ERROR: run inside the Unity project (ProjectSettings/ProjectVersion.txt not found)." >&2
    exit 2
fi
cd "$root" || exit 2

guid_of() { grep -m1 '^guid:' "$1.meta" 2>/dev/null | tr -d '\r' | awk '{print $2}'; }

# ---------- 1. Build scene list ----------
mapfile -t scene_paths < <(tr -d '\r' < "$BUILD_SETTINGS" | awk '
    /^  - enabled:/ { enabled = $3 }
    /^    path:/    { if (enabled == 1) print $2 }')
if [ ${#scene_paths[@]} -eq 0 ]; then
    fail "No enabled scenes in $BUILD_SETTINGS (the CrazySDK builder aborts)."
else
    info "Build scenes: ${scene_paths[*]}"
    case "${scene_paths[0]}" in
        */"$BOOTSTRAP_SCENE_NAME".unity) pass "Build index 0 is $BOOTSTRAP_SCENE_NAME." ;;
        *) fail "Build index 0 is ${scene_paths[0]}, expected $BOOTSTRAP_SCENE_NAME (it initializes all systems)." ;;
    esac
fi
for scene in "${scene_paths[@]}"; do
    [ -f "$scene" ] || fail "Build scene missing on disk: $scene"
done

# ---------- 2. Test harness scripts inside build scenes ----------
harness_hits=0
for harness_dir in "${TEST_HARNESS_DIRS[@]}"; do
    for script in $(find "$harness_dir" -name "*.cs" 2>/dev/null); do
        [ -f "$script" ] || continue
        guid="$(guid_of "$script")"
        [ -n "$guid" ] || continue
        for scene in "${scene_paths[@]}"; do
            if [ -f "$scene" ] && grep -qF "guid: $guid" "$scene"; then
                fail "Test harness $(basename "$script") is in build scene $scene - remove it before release."
                harness_hits=$((harness_hits + 1))
            fi
        done
    done
done
[ $harness_hits -eq 0 ] && pass "No test harness scripts (${TEST_HARNESS_DIRS[*]}) in build scenes."

# ---------- 3. PlatformConfig targets CrazyGames ----------
enum_names="$(tr -d '\r' < "$PLATFORM_ENUM" | sed -n '/enum PlatformType/,/}/p' | sed 's://.*::' \
    | tr -d '{} \t' | grep -v '^enumPlatformType$' | tr '\n' ' ' | tr ',' '\n' | tr -d ' ' | grep -v '^$')"
expected_index="$(echo "$enum_names" | grep -nx "$TARGET_PLATFORM" | cut -d: -f1)"
actual_index="$(tr -d '\r' < "$PLATFORM_CONFIG" | awk '/^  platform:/ {print $2}')"
if [ -z "$expected_index" ] || [ -z "$actual_index" ]; then
    fail "Could not read PlatformType enum or $PLATFORM_CONFIG 'platform' value."
elif [ "$actual_index" -eq $((expected_index - 1)) ]; then
    pass "PlatformConfig.platform = $actual_index ($TARGET_PLATFORM)."
else
    fail "PlatformConfig.platform = $actual_index, expected $((expected_index - 1)) ($TARGET_PLATFORM)."
fi

# ---------- 4. Gameplay start/stop reported by game code (via GameplayStateManager) ----------
GAMEPLAY_STATE_MANAGER="Assets/_Core/Gameplay/GameplayStateManager.cs"
game_calls="$(grep -rlE '\.(BeginGameplay|EndGameplay)\(' --include=*.cs "${GAME_CODE_DIRS[@]}" 2>/dev/null \
    | grep -vE "$PLATFORM_LAYER_REGEX")"
direct_calls="$(grep -rlE '\.Game\.Gameplay(Start|Stop)\(' --include=*.cs "${GAME_CODE_DIRS[@]}" 2>/dev/null \
    | grep -vE "$PLATFORM_LAYER_REGEX" | grep -v "^$GAMEPLAY_STATE_MANAGER$")"
if [ -z "$game_calls" ]; then
    warn "No GameplayStateManager.BeginGameplay()/EndGameplay() calls in game code (CrazyGames expects gameplay start/stop)."
else
    pass "BeginGameplay/EndGameplay called in: $(echo "$game_calls" | tr '\n' ' ')"
fi
if [ -n "$direct_calls" ]; then
    warn "Direct Game.GameplayStart/Stop() calls bypass GameplayStateManager: $(echo "$direct_calls" | tr '\n' ' ')"
fi

# ---------- 5. UnityEditor usage in runtime code ----------
# A 'using UnityEditor' or 'UnityEditor.' reference must be inside #if UNITY_EDITOR or under an Editor/ folder.
editor_leaks="$(find "${GAME_CODE_DIRS[@]}" -name '*.cs' -not -path '*/Editor/*' -print0 2>/dev/null \
    | xargs -0 awk '
        FNR == 1 { depth = 0; guarded = 0 }
        /^[ \t]*#if[ \t]/   { depth++; if ($0 ~ /UNITY_EDITOR/ && $0 !~ /!UNITY_EDITOR/ && guarded == 0) guarded = depth }
        /^[ \t]*#endif/     { if (guarded == depth) guarded = 0; depth-- }
        /UnityEditor/ && !/^[ \t]*\/\// && !/^[ \t]*#/ { if (guarded == 0) print FILENAME ":" FNR }
    ' 2>/dev/null)"
if [ -n "$editor_leaks" ]; then
    fail "UnityEditor referenced outside #if UNITY_EDITOR (breaks the WebGL build): $(echo "$editor_leaks" | tr '\n' ' ')"
else
    pass "No unguarded UnityEditor references in runtime code."
fi

# ---------- 6. Debug-only code ----------
debug_input="$(grep -rlE 'Keyboard\.current|Input\.GetKey' --include=*.cs "${GAME_CODE_DIRS[@]}" 2>/dev/null \
    | grep -vE "^(Assets/_Project/Testing|Assets/_Platforms/[^/]+/Testing)/")"
[ -n "$debug_input" ] && warn "Raw keyboard input outside Testing (check it is not a debug shortcut): $(echo "$debug_input" | tr '\n' ' ')"
log_calls="$(grep -rhoE 'Debug\.Log(Warning|Error)?\(' --include=*.cs "${GAME_CODE_DIRS[@]}" 2>/dev/null | wc -l | tr -d ' ')"
info "$log_calls Debug.Log* calls in _Core/_Project (they run in release builds; remove noisy per-frame logs)."

# ---------- 7. CrazyGamesSettings ----------
setting() { tr -d '\r' < "$CRAZY_SETTINGS" | awk -v k="  $1:" 'index($0, k) == 1 {print $2}'; }
if [ ! -f "$CRAZY_SETTINGS" ]; then
    fail "$CRAZY_SETTINGS missing (the SDK loads it from Resources)."
else
    # PauseManager pauses every ad request and owns the time scale, so either value works.
    info "CrazyGamesSettings.pauseGameDuringAd = $(setting pauseGameDuringAd) (PauseManager pauses ads either way)."
    info "Editor-only simulation flags: alwaysThrowAdError=$(setting alwaysThrowAdError), disableAdPreviews=$(setting disableAdPreviews), disableSdkLogs=$(setting disableSdkLogs)."
fi

# ---------- 8. Size-related content ----------
mapfile -t resource_dirs < <(find Assets -type d -name Resources 2>/dev/null)
info "Resources folders (ship unconditionally): ${resource_dirs[*]}"
runtime_packages="$(tr -d '\r' < "$PACKAGE_MANIFEST" | grep -oE '"com\.[^"]+"[[:space:]]*:' | tr -d '":' | tr -d ' ' \
    | grep -vE "$EDITOR_ONLY_PACKAGES")"
info "Runtime packages - confirm each is used (unused packages add to wasm/data size): $(echo "$runtime_packages" | tr '\n' ' ')"

echo "---"
echo "Summary: $FAILS FAIL, $WARNS WARN"
[ $FAILS -eq 0 ] && exit 0 || exit 1
