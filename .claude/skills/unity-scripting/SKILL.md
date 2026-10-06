---
name: unity-scripting
description: C# conventions and Unity pitfalls for this template (2D and 3D) - file placement and namespaces, field naming, serialization, Unity fake-null, 2D vs 3D physics, Input System, Task-based async on WebGL, editor-only guards. Use when writing, editing or debugging any C# script in this project, even a one-line fix.
---

# Unity C# in this project

The other project skills (`core-systems`, `gameplay-feature`, `ui-development`, `platform-integration`) build on this one.

## Placement and style

- `Assets/_Core/<Folder>/` → namespace `_Core.<Folder>`; framework only, never `using _Project...`.
- `Assets/_Project/<Folder>/` → `_Project.<Folder>`, dropping `Scripts` (`_Project/Scripts/Gameplay/Enemy.cs` → `_Project.Gameplay`).
- `Assets/_Platforms/<Name>/` → `_Platforms.<Name>.*`: SDK bridges only (Assembly-CSharp); see `platform-integration`.
- Fields: `[SerializeField] private float moveSpeed`; private `_rigidbody`; constants `PascalCase`. Public fields only on plain config/data types (`GameConfig`, `PoolData`, `SaveData` subclasses), matching their existing style.
- XML `/// <summary>` on public types and members; code and comments in English.
- Don't hand-edit `.unity`/`.prefab`/`.asset`/`.meta` unless asked; see `unity-asset-editing`.

## Pitfalls worth remembering

- **Fake null.** Destroyed `UnityEngine.Object`s are `== null`, but `?.`, `??`, `is null` bypass that. Use `!= null` on Unity objects and on `Singleton<T>.Instance`.
- **Lifetime after await/callbacks.** A `Task` or SDK callback can complete after the component is destroyed (scene change during an ad). Check `if (this == null) return;` after each `await` and at the top of callbacks.
- **`async void`** only for Unity messages and UI handlers, wrapped in `try/catch`.
- **WebGL is single-threaded.** No `Task.Run`, threads, `.Result`, `.Wait()`, `Thread.Sleep`. Poll frames with `await Task.Yield()` and time with `Time.realtimeSinceStartup` (pauses and ads set `timeScale` to 0).
- **Serialization.** Rename serialized fields with `[FormerlySerializedAs]`; append enum values, never insert.
- **Overrides.** Call `base.Awake/OnEnable/OnDisable/OnDestroy` when overriding `Singleton<T>` or `GameplayPauseHandler`; after `base.Awake()` in a singleton, bail out `if (IsDuplicate)`.
- **Time scale.** Never write `Time.timeScale`: `PauseManager` owns it (pause sources, `PauseManager.TimeScale` for slow motion) and undoes other writes.
- **Subscriptions.** Pair every subscribe with an unsubscribe in the mirrored method (`OnEnable`/`OnDisable`, or `Init`/`OnDestroy` for persistent managers). Lambdas can't be unsubscribed.
- **Editor code** (`UnityEditor`) belongs in the `_Core.Editor` / `_Project.Editor` assemblies (`Assets/_Core/Editor/`, `Assets/_Project/Scripts/Editor/`) or inside `#if UNITY_EDITOR`, or the WebGL build fails. Other `Editor/` folders under `_Core`/`_Project` are not editor-only (asmdefs override the special folder).
- **Hot paths** matter more on WebGL (stop-the-world GC): cache lookups, avoid per-frame allocations, pool frequent spawns.

## 2D or 3D

The template serves both. Use the physics family the scene and prefab already use and never mix them on one object: `Rigidbody2D`/`Collider2D`/`OnTriggerEnter2D`/`Physics2D` versus `Rigidbody`/`Collider`/`OnTriggerEnter`/`Physics`. When there's no existing scene to follow, ask or pick from the user's description and say which. Unity 6 names apply to both: `linearVelocity`, `linearDamping`, `angularDamping` (not `velocity`/`drag`). The active profile is the `GAME_2D`/`GAME_3D` define; a 3D game must first run `Tools/Template/Set Up as 3D Game` (see CLAUDE.md), since the 2D profile has 3D physics on Script simulation.

## Input

New code uses the Input System only (`InputSystem.actions.FindAction("Player/Move")` cached in `Awake`, or a serialized `InputActionReference`). The project-wide asset is `Assets/Settings/InputSystem_Actions.inputactions` (maps `Player`, `UI`; schemes Keyboard&Mouse, Gamepad, Touch, Joystick, XR). Input callbacks keep firing at `timeScale = 0`, so gate them on your pause state. Escape/Back belongs to `UIManager` only.

## Async

`System.Threading.Tasks` with `TaskCompletionSource<T>` around callback APIs, `TrySet*` because SDKs may call back twice. No UniTask, no coroutine-based framework code.

## Done

Compile with `unity-verify`, and tell the user which components, Inspector fields or assets they must set up in the Editor.
