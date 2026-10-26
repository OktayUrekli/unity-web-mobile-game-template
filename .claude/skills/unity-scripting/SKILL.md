---
name: unity-scripting
description: C# conventions and Unity pitfalls in this template (2D and 3D) - placement and namespaces, fake null, lifetime after await, WebGL threading, serialization, 2D vs 3D physics, Input System, editor-only code. Use when writing, editing or debugging C# here, even a one-line fix.
---

# Unity C# in this project

Naming, async and `timeScale` rules are in CLAUDE.md. The other project skills build on this one.

## Placement

- `Assets/_Core/<Folder>/` → `_Core.<Folder>`; framework only, never `using _Project...`.
- `Assets/_Project/<Folder>/` → `_Project.<Folder>`, dropping `Scripts` (`_Project/Scripts/Gameplay/Enemy.cs` → `_Project.Gameplay`).
- `Assets/_Platforms/<Name>/` → `_Platforms.<Name>.*`: SDK bridges only (`platform-integration`).
- Public fields only on plain data types (`GameConfig`, `PoolData`, `SaveData` subclasses), matching their style.
- Scenes, prefabs, assets and `.meta` files: `unity-asset-editing`.

## Pitfalls

- **Fake null.** Destroyed `UnityEngine.Object`s are `== null`, but `?.`, `??` and `is null` skip that check. Use `!= null` on Unity objects and on `Singleton<T>.Instance`.
- **Lifetime.** A `Task` or SDK callback can finish after its component is destroyed (a scene change during an ad). Check `if (this == null) return;` after each `await` and at the top of callbacks.
- **`async void`** only for Unity messages and UI handlers, wrapped in `try/catch`.
- **WebGL is single-threaded.** Poll with `await Task.Yield()`; measure time with `Time.realtimeSinceStartup`, since pauses and ads set `timeScale` to 0.
- **Serialization.** Rename serialized fields with `[FormerlySerializedAs]`; append enum values, never insert.
- **Overrides.** Call `base.Awake/OnEnable/OnDisable/OnDestroy` when overriding `Singleton<T>` or `GameplayPauseHandler`.
- **Subscriptions.** Pair every subscribe with an unsubscribe in the mirrored method (`OnEnable`/`OnDisable`, or `Init`/`Shutdown` for managers). Lambdas can't be unsubscribed.
- **Editor code** (`UnityEditor`) goes in `Assets/_Core/Editor/` or `Assets/_Project/Scripts/Editor/`, or inside `#if UNITY_EDITOR`; otherwise the player build fails.
- **Hot paths** matter more on WebGL (stop-the-world GC): cache lookups, no per-frame allocations, pool frequent spawns.

## 2D or 3D

Use the physics family the scene and prefab already use, and never mix them on one object (`Rigidbody2D`/`Collider2D`/`OnTriggerEnter2D` versus `Rigidbody`/`Collider`/`OnTriggerEnter`). With nothing to follow, pick from the user's description and say which. Unity 6 names: `linearVelocity`, `linearDamping`, `angularDamping`. The active profile is the `GAME_2D`/`GAME_3D` define; a 3D game first runs `Tools/Template/Set Up as 3D Game`, since the 2D profile runs 3D physics on Script simulation. NavMesh (`com.unity.ai.navigation`) isn't installed; add it if a game needs it.

## Input

Input System only: `InputSystem.actions.FindAction("Player/Move")` cached in `Awake`, or a serialized `InputActionReference`. The project-wide asset is `Assets/Settings/InputSystem_Actions.inputactions`. Input callbacks keep firing at `timeScale = 0`. Escape/Back belongs to `UIManager`.

## Async

`TaskCompletionSource<T>` around callback APIs, with `TrySet*` because SDKs may call back twice.

## Done

Compile with `unity-verify`, then tell the user which components, Inspector fields or assets they must set up in the Editor.
