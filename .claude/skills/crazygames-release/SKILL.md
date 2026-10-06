---
name: crazygames-release
description: Pre-release checklist for shipping a CrazyGames WebGL build of this Unity template - runs file-based preflight checks (test harnesses in build scenes, build scene order, PlatformConfig target, gameplay start/stop via GameplayStateManager, unguarded UnityEditor code, debug input, CrazyGamesSettings, Resources and packages), a batch-mode compile, then walks the user through the Release Build, Analyzer size check, local QA and portal upload, ending with a pass/fail report. Invoked manually with /crazygames-release.
disable-model-invocation: true
argument-hint: "[notes, e.g. mobile or skip-compile]"
---

# CrazyGames release checklist

Arguments: `$ARGUMENTS` (optional). If it mentions "mobile", treat mobile support as required
(initial load < 20 MB, "Runs on mobile web" ticked). If it says "skip-compile", skip step 2 and
mark it NOT RUN in the report.

CrazyGames facts here come from the `webgl-performance` skill and its references (verified against
the SDK source). Portal policies not visible in code (ad frequency, QA rules) live at
docs.crazygames.com; point the user there rather than inventing rules.

Copy this checklist into your reply and tick items as you go:

```
Release checklist
- [ ] 1. Preflight file checks (scripts/preflight.sh)
- [ ] 2. Compile check (unity-verify)
- [ ] 3. User: CrazySDK/Release Build
- [ ] 4. User: Analyzer size and asset check
- [ ] 5. User: local QA on a Development build
- [ ] 6. User: upload Builds/CrazyGamesRelease and run portal QA
```

## 1. Preflight file checks

```
bash .claude/skills/crazygames-release/scripts/preflight.sh
```

Exit 0 = no FAIL, 1 = at least one FAIL. Output is one line per check:

| Check | FAIL / WARN means | Fix |
|---|---|---|
| Build scenes | index 0 is not `00_Bootstrap`, or a scene file is missing | Reorder/fix `EditorBuildSettings.asset` (`unity-asset-editing` skill) |
| Test harnesses | a script from `Assets/_Project/Testing/` or `Assets/_Platforms/CrazyGames/Testing/` sits in a build scene | Remove the component/GameObject (`unity-asset-editing` recipes) or ask the user to delete it in the Editor |
| PlatformConfig | `platform` is not the `CrazyGames` enum index | Select it with `Tools/Template/Platform/CrazyGames` (sets `Assets/_Project/ScriptableObjects/Config/PlatformConfig.asset` and the define) |
| Gameplay start/stop | no game code calls `GameplayStateManager.Instance.BeginGameplay/EndGameplay()`, or code calls `Game.GameplayStart/Stop()` directly | Call them at level start and game over (`gameplay-feature` skill; pauses are reported automatically); the platform `GameplayStart` also marks the end of QA's load-size measurement |
| UnityEditor | `UnityEditor` referenced outside `#if UNITY_EDITOR`/`Editor/` | Guard it; the WebGL player build fails otherwise |
| Debug input | raw `Keyboard.current`/`Input.GetKey` outside `Testing/` | Confirm it is gameplay input, not a cheat/debug key (expected: `UIManager` Back/Escape, `GameplayController` Space to score) |
| CrazyGamesSettings | file missing | Re-import the SDK; `pauseGameDuringAd` is reported as INFO only (`PauseManager` pauses ads either way) |

`BuildValidator` (`Assets/_Project/Scripts/Editor/BuildValidator.cs`, also `Tools/Build/Validate Build Settings`) repeats the blocking checks before every player build: index 0 must be `00_Bootstrap`, no test scene or harness, `PlatformConfig.Platform` must match the target and its `PLATFORM_X` define, and the CrazyGames SDK must not be compiled into another platform's WebGL build. It warns when the profile define is missing, and after the build when the other profile's physics module is included.

INFO lines (Debug.Log count, Resources folders, runtime packages, Editor-only SDK simulation flags)
are for the report; mention anything that looks unused or noisy, but they do not fail the release.

Feedback loop: fix every FAIL (ask the user before editing scenes), rerun preflight until it
reports 0 FAIL. WARNs may ship only if the user explicitly accepts them.

## 2. Compile check

Run the `unity-verify` skill (compile-only). The Editor must be closed; if it is open, ask the user
to close it or to confirm the Console shows no red errors. Fix errors and rerun until it passes.

## 3-6. Manual steps for the user

Give these to the user in Turkish, numbered, and wait for their results:

3. **Release Build**: open the project in Unity 6000.3.21f1. If mobile matters, open
   `CrazySDK/Go to Build` and tick "Runs on mobile web". Save the open scene, then
   `CrazySDK/Release Build`. It deletes and rebuilds `Builds/CrazyGamesRelease`, runs several full
   player builds (plus ASTC and, with mobile, 512/1024 MB memory variants) and can take a long
   time. Do not use File > Build Profiles for the upload build.
4. **Analyzer** (opens after the build, or `CrazySDK/Go to Analyzer`; it opens every build scene):
   - total size must be <= 250 MB (otherwise not accepted);
   - initial load size: > 50 MB may be rejected, > 20 MB may be disabled on mobile;
   - resolve flagged items: textures > 1024 px, long non-mono/high-quality audio, Read/Write or
     mipmapped textures, missing scripts in build scenes.
   Ask the user to paste the total and initial load sizes.
5. **Local QA** with `CrazySDK/Development Build` (runs on localhost, where the real SDK loads;
   development builds do not work on CrazyGames itself):
   - first click/tap starts audio;
   - midgame and rewarded ads pause gameplay and mute audio, and resume afterwards;
   - a rewarded ad grants its reward once, only on `AdResult.Completed`;
   - with an adblocker enabled the game does not freeze and ad failures resume play;
   - SDK log lines such as `Gameplay start called` appear at the right moments (dev builds log them);
   - progress survives a page reload;
   - if mobile matters: test in a phone browser or device emulation (touch input, portrait/landscape,
     UI scaling).
6. **Upload**: upload the entire `Builds/CrazyGamesRelease` folder in the CrazyGames developer
   portal, then run the portal's QA/preview checks (gameplay start/stop, ads, adblock, mobile)
   following the current requirements on docs.crazygames.com.

## Report

End with a short report (fill in real results; never mark an unrun step as PASS):

```
CrazyGames release report
1. Preflight:      PASS | FAIL (<n> FAIL, <n> WARN) - <main items>
2. Compile:        PASS | FAIL | NOT RUN (<reason>)
3. Release build:  PENDING (user) | DONE
4. Analyzer:       PENDING | total <x> MB, initial <y> MB - PASS | WARN | FAIL
5. Local QA:       PENDING | PASS | FAIL (<item>)
6. Upload/QA:      PENDING | DONE
Verdict: READY | NOT READY - <blocking items>
```
