#!/usr/bin/env bash
# Look up Unity asset GUIDs and the files that reference them.
#
# Usage (run from anywhere inside the Unity project):
#   find-guid.sh <asset-path>        Print the GUID stored in <asset-path>.meta
#   find-guid.sh --refs <guid>       List serialized files under Assets/ and ProjectSettings/ that reference <guid>
#   find-guid.sh --refs <asset-path> Same, resolving the GUID from the asset's .meta first
#
# Exit codes: 0 = found, 1 = nothing found, 2 = usage or environment error.

set -u

# Serialized asset types that can hold GUID references.
REF_EXTENSIONS=(unity prefab asset mat controller overrideController anim playable spriteatlas spriteatlasv2 inputactions)
GUID_PATTERN='^[0-9a-f]{32}$'

usage() {
    sed -n '2,9p' "$0" | sed 's/^# \{0,1\}//'
    exit 2
}

# Walk up from the current directory to the folder containing ProjectSettings/ProjectVersion.txt.
find_project_root() {
    local dir
    dir="$(pwd)"
    while [ -n "$dir" ] && [ "$dir" != "/" ]; do
        if [ -f "$dir/ProjectSettings/ProjectVersion.txt" ]; then
            echo "$dir"
            return 0
        fi
        dir="$(dirname "$dir")"
    done
    return 1
}

guid_of() {
    local asset="$1"
    local meta="${asset%.meta}.meta"
    if [ ! -f "$meta" ]; then
        echo "ERROR: no .meta file at '$meta' (Unity creates it on import; open the Editor once)." >&2
        exit 2
    fi
    local guid
    guid="$(grep -m1 '^guid:' "$meta" | tr -d '\r' | awk '{print $2}')"
    if [ -z "$guid" ]; then
        echo "ERROR: '$meta' has no guid line." >&2
        exit 2
    fi
    echo "$guid"
}

list_refs() {
    local guid="$1"
    if ! [[ "$guid" =~ $GUID_PATTERN ]]; then
        echo "ERROR: '$guid' is not a 32-char lowercase hex GUID." >&2
        exit 2
    fi
    local root
    if ! root="$(find_project_root)"; then
        echo "ERROR: run this inside a Unity project (ProjectSettings/ProjectVersion.txt not found)." >&2
        exit 2
    fi
    local includes=()
    local ext
    for ext in "${REF_EXTENSIONS[@]}"; do includes+=("--include=*.$ext"); done

    local results
    results="$(cd "$root" && grep -rlF "guid: $guid" "${includes[@]}" Assets ProjectSettings 2>/dev/null | sort)"
    if [ -z "$results" ]; then
        echo "No references to $guid in Assets/ or ProjectSettings/."
        exit 1
    fi
    local file count
    while IFS= read -r file; do
        count="$(cd "$root" && grep -cF "guid: $guid" "$file")"
        echo "$file ($count)"
    done <<< "$results"
}

[ $# -eq 0 ] && usage

case "$1" in
    -h|--help) usage ;;
    --refs)
        [ $# -ne 2 ] && usage
        target="$2"
        if [[ "$target" =~ $GUID_PATTERN ]]; then
            list_refs "$target"
        else
            resolved="$(guid_of "$target")" || exit 2
            list_refs "$resolved"
        fi
        ;;
    *)
        [ $# -ne 1 ] && usage
        [ -e "$1" ] || [ -e "$1.meta" ] || { echo "ERROR: '$1' does not exist." >&2; exit 2; }
        guid_of "$1"
        ;;
esac
