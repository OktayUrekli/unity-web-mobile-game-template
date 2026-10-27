---
name: unity-asset-editing
description: Safely changes scenes, prefabs, config and SoundData assets, build settings and .meta files - Unity MCP when the Editor is open, careful YAML edits otherwise - and finds which assets reference a script. Use for any scene, prefab, asset or meta change, or a missing script or broken GUID.
---

# Editing scenes, prefabs and assets

## Editor open: use Unity MCP

With `mcp__unity-mcp__Unity_RunCommand` available, change scenes, prefabs, configs and build settings through Unity's APIs, not YAML: the Editor keeps fileIDs, GUIDs, prefab links and `.meta` files consistent. Typical APIs: `EditorSceneManager.OpenScene/SaveScene`, `PrefabUtility.LoadPrefabContents/SaveAsPrefabAsset`, `AssetDatabase.LoadAssetAtPath` + `SerializedObject` (private serialized lists) + `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets`, `EditorBuildSettings.scenes`.

- Follow the tool's template (`internal class CommandScript : IRunCommand`) and call `result.RegisterObjectModification(obj)` before changing an object, so it can be undone. The tool rejects `System.IO` and reflection: log results instead of writing files.
- Save explicitly; files on disk (what git sees) change only when saved.
- Say so before switching the open scene or entering Play Mode; the user may have unsaved work.
- Verify: `git diff` the asset, then check the Console (`Unity_GetConsoleLogs`) for missing-script or serialization errors.

## Editor closed: YAML

Hand-editing text YAML is safe only if every cross-reference stays consistent. Read `references/yaml-fallback.md` first (structure, fileIDs, recipes for removing components and objects, adding config entries and build scenes). When in doubt, stop and give the user Editor steps.

## Finding GUIDs and references

```
bash .claude/skills/unity-asset-editing/scripts/find-guid.sh <asset-path>          # GUID from its .meta
bash .claude/skills/unity-asset-editing/scripts/find-guid.sh --refs <asset-path>   # serialized files referencing it
bash .claude/skills/unity-asset-editing/scripts/find-guid.sh --refs <32-hex-guid>
```

`--refs` prints `path (count)` for scenes, prefabs, assets, materials, controllers and other serialized files under `Assets/` and `ProjectSettings/`. Exit 1 = no references, 2 = bad input. Works with or without the Editor.

## .meta and GUID rules

- Never write a `.meta` by hand for anything Unity can import; Unity creates it on the next import.
- Never change an existing GUID. Move or rename an asset together with its `.meta` (`git mv file file.meta`); a lost meta means a new GUID and "missing script" everywhere.
- Before deleting a script or asset, run `find-guid.sh --refs` on it and clean those references first.
- Keep each file's line endings and indentation (this repo mixes LF scene files and CRLF `.meta` files; check with `file <path>`). Don't reformat or reorder documents you aren't changing.
- Third-party folders are off limits (CLAUDE.md).

## Leave it to the Editor

Give the user numbered Editor steps (in Turkish) instead of hand-writing:

- a new scene, prefab or ScriptableObject asset (Project window > right-click > Create);
- UI hierarchies (Canvas, anchors, layout groups, Button events);
- TextMeshPro font assets;
- components with many serialized defaults (Camera, Light, Rigidbody, colliders, renderers, Animator);
- nested prefabs and prefab overrides, or anything whose fileIDs don't exist in the file yet.

After that you can wire simple values in YAML if needed.

## After editing

Run `unity-verify` (it imports assets and fails loudly on broken scripts). For hand edits, ask the user to open the scene or prefab and check the Console for "missing script" or "broken prefab" warnings; a hand edit can't be checked visually from the command line.
