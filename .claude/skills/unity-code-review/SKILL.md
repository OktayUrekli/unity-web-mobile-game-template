---
name: unity-code-review
description: Reviews C# in this project (Assets/_Core, _Project, _Platforms) for lifetime, async, event, architecture, platform-layer and WebGL problems, and owns the known-issues list. Use for any review or audit of C# here; takes precedence over generic and plugin review skills.
---

# Code review for this project

Review as a senior Unity developer would; this file lists only what is specific to this codebase. The rules themselves live in CLAUDE.md and the other skills.

`references/known-issues.md` lists current defects. Read it first so you don't report a known problem as new, and re-check the code before citing an entry.

## Workflow

1. Scope: by default `git diff`, `git diff --staged` and untracked `.cs` under `Assets/_Core`, `Assets/_Project` and `Assets/_Platforms`. For "review X", all of X; tag known issues "(pre-existing)".
2. Read whole files and the `_Core` types they use; lifetime bugs don't show in a hunk.
3. Every finding points at a line you read. Lower the confidence when it depends on runtime, SDK or Inspector state you can't see.
4. A review changes no file unless asked. Put proposed `known-issues.md` changes in the report (an entry the change fixes, a pre-existing defect worth tracking); the file's header says when to apply them.

## What tends to go wrong here

- **Lifetime:** code after an `await` or in an SDK/ad callback touching a destroyed component; `?.`/`??` on Unity objects or `Singleton.Instance`; singletons used before bootstrap or in `OnDestroy` during quit.
- **Async:** `async void` outside Unity messages and UI handlers; un-awaited Tasks swallowing exceptions; anything that waits forever for an ad or SDK callback.
- **Events:** unpaired `EventBus.Subscribe`, lambda subscriptions, missing `base.` calls in `Singleton`/`GameplayPauseHandler` overrides, `EventBus.Clear()` outside the Bootstrapper.
- **Ads and rewards:** a reward outside the `AdResult.Completed` branch; a flow that stops on a non-`Completed` interstitial result.
- **Pause:** a `Time.timeScale` write outside `PauseManager`; a `PauseSource` never resumed; input not gated on `IsPaused`; `BeginGameplay` from `Awake` or around ads.
- **Content wiring:** a UI prefab missing from `UIConfig`, a scene missing from Build Settings or `GameScenes`, an unassigned `SoundData`, a pool id without registration; enum values inserted instead of appended.
- **Pools and saves:** double release, use after release, ignoring a null `Get`; save classes `JsonUtility` can't read, renamed fields without migration, saving every frame.
- **Architecture:** `_Core` referencing `_Project` or game content; SDK calls outside `Assets/_Platforms/<Name>/`; a service without a Null implementation or missing on a platform; `PlatformUser.Id` used as server identity; game tuning in `GameConfig`; runtime state written into ScriptableObjects; mixed 2D and 3D physics on one object.
- **WebGL:** threads, `.Result`, `.Wait()`, `Thread.Sleep`; `System.IO` for game data; reflection that stripping can break; allocations, LINQ, `Find*` or `GetComponent` in per-frame paths; `Instantiate`/`Destroy` for frequent spawns.
- **Unity:** renamed serialized fields without `[FormerlySerializedAs]`; assets moved without `.meta`; `UnityEditor` outside editor code; legacy `UnityEngine.Input`.

Useful greps: `async void`, `EventBus\.(Un)?[Ss]ubscribe`, `CrazySDK\.` outside the CrazyGames folder, `using _Project` in `_Core`, `Task\.Run|\.Result\b|\.Wait\(|Thread\.Sleep`, `File\.|Directory\.`, `Find(Any|First)ObjectByType|GameObject\.Find`, `Time\.timeScale`, `Instance\?\.`, `Input\.Get`, `SceneManager\.LoadScene`.

## Output

Open with a one-paragraph verdict (safe to merge / needs changes / blocking), then findings by severity:

```
### [Critical] Short title
- **Where:** Assets/_Core/Bootstrap/Bootstrapper.cs:33
- **Issue:** what is wrong, referencing the code.
- **Why it matters:** the concrete failure.
- **Fix:** the smallest change that resolves it.
- **Confidence:** Medium (needs runtime check: ...) - omit when High.
```

Critical = reachable correctness bug, data loss, crash, wrong reward. Major = architecture violation, WebGL breakage, latent bug under a realistic condition. Minor = hot-path cost or maintainability. Nit = style; batch them, skip when there are Critical findings. End with "Pre-existing issues touched by this change" and "Not verified". Write in the user's language; keep identifiers and these labels as they are.
