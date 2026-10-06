---
name: unity-asset-editing
description: Safely changes Unity scenes, prefabs, ScriptableObject configs (UIConfig, SceneConfig, AudioConfig, PoolConfig, PlatformConfig) and SoundData assets, build settings and .meta files - through Unity MCP when the Editor is open, otherwise by careful YAML edits - and finds which assets reference a script (scripts/find-guid.sh). Use whenever a task changes a scene, prefab, config asset, build list or meta file, or mentions a missing script or broken GUID.
---

# Editing Unity scenes, prefabs and assets

## Prefer the Editor via Unity MCP

When `mcp__unity-mcp__Unity_RunCommand` is available (Editor open, MCP connected), make scene,
prefab, config and build-settings changes through Unity's own APIs instead of editing YAML: the
Editor keeps fileIDs, GUIDs, prefab links and `.meta` files consistent for you. Typical APIs:
`EditorSceneManager.OpenScene` / `SaveScene`, `Object.DestroyImmediate` via `result.DestroyObject`,
`PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`, `AssetDatabase.LoadAssetAtPath` +
`SerializedObject` (for private `[SerializeField]` lists) + `EditorUtility.SetDirty` +
`AssetDatabase.SaveAssets`, and `EditorBuildSettings.scenes`.

- Follow the tool's template (`internal class CommandScript : IRunCommand`) and call
  `result.RegisterObjectModification(obj)` before changing an object so the change is undoable.
- Save explicitly (scene or assets); an unsaved change is lost when the Editor closes, and the
  files on disk (what git and other tools see) do not change until saved.
- Do not switch the open scene or enter Play Mode without saying so; the user may have unsaved work.
- Verify: read the change back from disk (`git diff` on the asset), then check the Console
  (`mcp__unity-mcp__Unity_GetConsoleLogs`) for missing-script or serialization errors.
- `find-guid.sh` stays useful for read-only questions ("which scenes use this script") with or
  without MCP.

The rest of this skill is the fallback for when the Editor is closed: hand-editing text YAML
(Force Text serialization). It is safe if every cross-reference stays consistent; when in doubt,
stop and ask the user to make the change in the Editor (last section).

## Structure: documents, fileIDs, GUIDs

Each object is one YAML document with header `--- !u!<classID> &<fileID>`. The fileID is local to
the file. Class IDs seen in this project: `1` GameObject, `4` Transform, `224` RectTransform,
`20` Camera, `81` AudioListener, `114` MonoBehaviour, `1001` PrefabInstance,
`1660057539` SceneRoots (Unity 6 scene root list), `29/104/157/196` scene settings.

Real example from `Assets/_Project/Scenes/Test/99_Test.unity`:

```yaml
--- !u!1 &106640247
GameObject:
  m_Component:
  - component: {fileID: 106640249}   # Transform
  - component: {fileID: 106640248}   # CrazyGamesAdsTest
  - component: {fileID: 106640250}   # SaveTest
  m_Name: TestHarness
--- !u!114 &106640250
MonoBehaviour:
  m_GameObject: {fileID: 106640247}  # back-link to the owner
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: a4eac8d987b6ffa4fb8360b5910cff7c, type: 3}
  m_EditorClassIdentifier: Assembly-CSharp::SaveTest
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_Roots:
  - {fileID: 2100152125}   # root Transforms and root PrefabInstances
  - {fileID: 106640249}
```

- References inside the same file: `{fileID: N}`. Cross-file: `{fileID: N, guid: G, type: 2|3}`.
- `m_Script: {fileID: 11500000, guid: <GUID from the .cs.meta>, type: 3}` binds a MonoBehaviour
  or ScriptableObject to its script. Serialized fields follow as plain keys (`scenes:`, `platform:`).
- Hierarchy: each Transform has `m_Father: {fileID: <parent Transform>}` and
  `m_Children: [- {fileID: <child Transform>}]`. Root objects have `m_Father: {fileID: 0}` and are
  listed in `SceneRoots.m_Roots` (scenes only; prefabs have one root and no SceneRoots).
- A `PrefabInstance` (`!u!1001`) stores only overrides (`m_Modifications`) plus
  `m_SourcePrefab: {fileID: 100100000, guid: <prefab guid>, type: 3}`. Documents tagged
  `stripped` are placeholders for objects inside that instance.
- Enums serialize as ints in declaration order (e.g. `PlatformConfig.asset` has `platform: 2`
  = `PlatformType.CrazyGames`: None, GooglePlay, CrazyGames, ...).
- ScriptableObject `.asset` files are a single `--- !u!114 &11400000` MonoBehaviour document.

## Finding GUIDs and references

```
bash .claude/skills/unity-asset-editing/scripts/find-guid.sh Assets/_Project/Testing/SaveTest.cs
#  -> a4eac8d987b6ffa4fb8360b5910cff7c
bash .claude/skills/unity-asset-editing/scripts/find-guid.sh --refs Assets/_Project/Testing/SaveTest.cs
#  -> Assets/_Project/Scenes/Test/99_Test.unity (1)
bash .claude/skills/unity-asset-editing/scripts/find-guid.sh --refs <32-hex-guid>
```

`--refs` searches `*.unity *.prefab *.asset *.mat *.controller` (and other serialized types) under
`Assets/` and `ProjectSettings/`, printing `path (count)`. Exit 1 = no references, 2 = bad input.
To find a fileID inside one file: `grep -n "fileID: 106640250" <file>`.

## Recipe: remove a MonoBehaviour component

1. Find the component document: grep the script GUID in the scene/prefab; note its `&<fileID>` and
   `m_GameObject` fileID.
2. Delete the whole document, from its `--- !u!114 &<fileID>` line up to (not including) the next
   `--- ` line.
3. In the owner GameObject document, delete the line `- component: {fileID: <fileID>}`.
4. `grep -n "fileID: <fileID>"` in the file: any remaining hit is another component referencing
   it (a serialized field or UnityEvent target). Set those to `{fileID: 0}` or ask the user.
5. If the component lives inside a `PrefabInstance`, don't edit the scene - edit the prefab asset
   itself, or ask the user (the scene only holds overrides).

## Recipe: remove a GameObject

Collect every fileID first, then delete in one pass:
1. The GameObject document and every component listed in its `m_Component` (Transform included).
2. Recursively the same for each child: its Transform's `m_Children` entries point to child
   Transforms; each child Transform's `m_GameObject` gives the child GameObject.
3. In the parent Transform, delete the `- {fileID: <this Transform>}` line from `m_Children`
   (if the list becomes empty, write `m_Children: []`). If it was a root, delete its line from
   `SceneRoots.m_Roots` instead.
4. Grep every collected fileID; clear leftover references as in step 4 above.
5. A root prefab instance is removed by deleting its `PrefabInstance` document, all `stripped`
   documents whose `m_PrefabInstance` points to it, and its `SceneRoots.m_Roots` entry.

Example: removing the `TestHarness` object from `99_Test.unity` means removing GameObject
`106640247`, its Transform `106640249`, MonoBehaviours `106640248/50/51/52/53` (four harnesses +
`BootstrapGuard`) and the `106640249` line in `SceneRoots.m_Roots`. Test harnesses belong only in
`99_Test` (not in Build Settings); if one shows up in a build scene, remove it with the recipes above.

## Recipe: add an entry to a config list

Read the C# type first to know the field names. Config assets live in
`Assets/_Project/ScriptableObjects/Config/`. `UIConfig.asset` (type `UIConfig` -> `List<UIData>` with
`GameObject prefab`; the prefab is found by the `UIScreen`/`UIPopup` type on its root):

```yaml
  uiList:
  - prefab: {fileID: 1487212396533206050, guid: b07c76a103d7a92459fae01f38c9df1f, type: 3}
  - prefab: {fileID: <root GameObject fileID>, guid: <guid from the .prefab.meta>, type: 3}
```

- Match the existing indentation exactly (list items at the same indent as the key here).
- Fields holding asset references need the target's GUID and fileID
  (`{fileID: 11400000, guid: ..., type: 2}` for a ScriptableObject such as a `SoundData`,
  `{fileID: 8300000, ..., type: 3}` for an AudioClip, a prefab root's `{fileID: <root GameObject fileID>, ..., type: 3}`).
  Copy the pattern from an existing entry of the same kind in the same file; if none exists, ask
  the user to assign it in the Inspector.
- A new `SoundData` asset is easiest to create in the Editor (MCP: `ScriptableObject.CreateInstance<SoundData>()`
  + `AssetDatabase.CreateAsset`, then set `clip`/`volume`/`pitch`/`loop` through `SerializedObject`).

## Recipe: add a scene to the build

`ProjectSettings/EditorBuildSettings.asset`:

```yaml
  m_Scenes:
  - enabled: 1
    path: Assets/_Project/Scenes/Bootstrap/00_Bootstrap.unity
    guid: 5b58272a8d0a8ba46bf4544382154794
```

Append `- enabled: 1 / path: <scene path> / guid: <guid from the scene's .unity.meta>`.
`00_Bootstrap` must stay first (build index 0). Order matters also for `SceneLoader`'s
load-next-by-index helper. The scene's `.meta` must already exist (Unity created it); never invent
the GUID. Code loads it by name: add the name to `_Project.Systems.GameScenes` (and set `SceneConfig.firstScene` if it is the first scene after boot).

## .meta and GUID rules

- Never create `.meta` files by hand for anything Unity can import. A new `.cs`, texture, audio or
  folder gets its `.meta` on the next import (Editor focus or a `unity-verify` run).
- Never change an existing GUID, and always move/rename an asset together with its `.meta`
  (`git mv file file.meta`); a lost meta means a new GUID and "missing script" everywhere.
- Before deleting a script, run `find-guid.sh --refs` on it and clean those references first.
- Preserve line endings and indentation: check with `file <path>` (this repo mixes LF scene files
  and CRLF `.meta` files) and keep what the file already uses. Two-space YAML indent, no tabs.
- Do not reformat, reorder or "clean up" documents you are not changing; Unity rewrites them.
- Do not edit files under `Assets/CrazySDK/`, `Assets/ThirdParty/`, `Assets/Plugins/` (except the template's `CrazyGames.SDK*.asmdef` files and `crazySDK.jslib.meta`).

## Validate after editing

1. Re-read the edited region; `grep -c "^--- "` before and after should differ by exactly the
   number of documents you removed/added.
2. Grep each removed fileID/GUID - expect no hits (except intentional ones).
3. Run the `unity-verify` skill (it imports assets in batch mode and fails loudly on broken
   scripts). Rerun after every fix until clean.
4. Tell the user what you changed and ask them to open the scene/prefab in the Editor and check
   the Console for "missing script", "The referenced script ... is missing" or "broken prefab"
   warnings, and that the objects look right in the Hierarchy/Inspector. Hand edits cannot be
   visually verified from the CLI.

## When to stop and use the Editor

Do not hand-write these; give the user numbered Editor steps instead (in Turkish), then wire the
resulting asset by editing YAML only if needed:
- Creating a new scene, prefab, or ScriptableObject asset from scratch (new fileIDs, serialized
  defaults). Steps: Project window > right-click target folder > Create > Scene / Prefab /
  `Configuration/...` menu; save; tell me when done.
- Building UI hierarchies (Canvas, RectTransform anchors, layout groups, Button events).
- TextMeshPro font assets (Essential Resources are already imported under `Assets/TextMesh Pro`).
- Adding components with many serialized defaults (Camera, Light, Rigidbody/Rigidbody2D, colliders,
  SpriteRenderer/MeshRenderer, Animator) - the user adds via Inspector > Add Component, then you can adjust simple values.
- Changing prefab overrides/nested prefabs, or anything where the fileIDs you need do not exist
  in the file yet.
