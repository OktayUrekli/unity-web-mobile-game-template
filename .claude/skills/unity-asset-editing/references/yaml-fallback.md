# Hand-editing Unity YAML (Editor closed)

Text serialization only. Safe while every cross-reference stays consistent; when in doubt, give the user Editor steps instead (SKILL.md, "Leave it to the Editor").

## Structure

Each object is one YAML document with the header `--- !u!<classID> &<fileID>`; the fileID is local to the file. Common class IDs: `1` GameObject, `4` Transform, `224` RectTransform, `20` Camera, `81` AudioListener, `114` MonoBehaviour, `1001` PrefabInstance, `1660057539` SceneRoots (Unity 6 root list), `29/104/157/196` scene settings.

Illustrative shape (fileIDs and GUIDs differ in every file; read the real ones):

```yaml
--- !u!1 &106640247
GameObject:
  m_Component:
  - component: {fileID: 106640249}   # Transform
  - component: {fileID: 106640250}   # a MonoBehaviour
  m_Name: SomeObject
--- !u!114 &106640250
MonoBehaviour:
  m_GameObject: {fileID: 106640247}  # back-link to the owner
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: <GUID from the .cs.meta>, type: 3}
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_Roots:
  - {fileID: 106640249}              # root Transforms and root PrefabInstances
```

- Same-file reference: `{fileID: N}`. Cross-file: `{fileID: N, guid: G, type: 2|3}`.
- `m_Script` binds a MonoBehaviour or ScriptableObject to its script; serialized fields follow as plain keys.
- Hierarchy: a Transform has `m_Father` (parent Transform, `{fileID: 0}` for roots) and `m_Children`. Scene roots are listed in `SceneRoots.m_Roots`; a prefab has one root and no SceneRoots.
- A `PrefabInstance` stores only overrides (`m_Modifications`) and `m_SourcePrefab`. Documents tagged `stripped` are placeholders for objects inside it.
- Enums serialize as ints in declaration order. Read the enum before writing one.
- A ScriptableObject `.asset` is a single `--- !u!114 &11400000` document.
- Find a fileID inside a file: `grep -n "fileID: <N>" <file>`.

## Remove a MonoBehaviour

1. Grep the script GUID in the scene or prefab; note the component's `&<fileID>` and its `m_GameObject`.
2. Delete the whole document, from `--- !u!114 &<fileID>` up to the next `--- ` line.
3. Delete `- component: {fileID: <fileID>}` from the owner GameObject.
4. Grep `fileID: <fileID>` again: remaining hits are serialized fields or UnityEvent targets. Set them to `{fileID: 0}` or ask the user.
5. If the component lives inside a `PrefabInstance`, edit the prefab asset (or ask), not the scene.

## Remove a GameObject

Collect every fileID first, then delete in one pass:

1. The GameObject and every component in its `m_Component` (Transform included).
2. Recursively each child (`m_Children` lists child Transforms; their `m_GameObject` is the child).
3. The `- {fileID: <Transform>}` line in the parent's `m_Children` (write `m_Children: []` if it empties), or its `SceneRoots.m_Roots` line for a root.
4. Grep every collected fileID and clear leftovers as above.
5. A root prefab instance: delete the `PrefabInstance` document, every `stripped` document whose `m_PrefabInstance` points to it, and its `SceneRoots` entry.

## Add an entry to a config list

Read the C# type first for field names. Config assets live in `Assets/_Project/ScriptableObjects/Config/`. Copy the pattern of an existing entry in the same file and match its indentation exactly:

```yaml
  uiList:
  - prefab: {fileID: <existing root GameObject fileID>, guid: <existing prefab guid>, type: 3}
  - prefab: {fileID: <root GameObject fileID>, guid: <guid from the new .prefab.meta>, type: 3}
```

Asset references need the target's GUID and fileID: `{fileID: 11400000, guid: ..., type: 2}` for a ScriptableObject such as `SoundData`, `{fileID: 8300000, ..., type: 3}` for an AudioClip, the root GameObject's fileID for a prefab. With no existing entry of that kind to copy, ask the user to assign it in the Inspector.

## Add a scene to the build

Append to `m_Scenes` in `ProjectSettings/EditorBuildSettings.asset`:

```yaml
  - enabled: 1
    path: <scene path>
    guid: <guid from the scene's .unity.meta>
```

`00_Bootstrap` stays first (build index 0). The `.meta` must already exist; never invent a GUID. Code loads scenes by name: add the name to `_Project.Systems.GameScenes`, and set `SceneConfig.firstScene` if it is the first scene after boot.

## Check the edit

1. Re-read the edited region. `grep -c "^--- "` before and after differs by exactly the number of documents removed or added.
2. Grep each removed fileID and GUID; expect no unintended hits.
3. Continue with "After editing" in SKILL.md.
