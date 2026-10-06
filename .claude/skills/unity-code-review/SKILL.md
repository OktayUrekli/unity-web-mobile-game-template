---
name: unity-code-review
description: Reviews C# in this template (Assets/_Core + Assets/_Project, 2D or 3D, CrazyGames/WebGL) for lifetime and async bugs, EventBus leaks, bootstrap ordering, platform-layer and architecture violations, WebGL constraints and hot-path cost, reports findings in a fixed format, and maintains the known-issues list. Use when asked to review, audit, check or refactor C# here (a diff, PR, branch, file or "my changes"), and before calling a non-trivial C# change finished.
---

# Code review for this project

Use your own judgement as a senior Unity reviewer; this file lists what is specific to this codebase. Compare designs against `core-systems`, `gameplay-feature`, `platform-integration`, `ui-development` and `webgl-performance`.

`references/known-issues.md` is the living list of current defects. Read it first so you recognise the same patterns and don't report pre-existing problems as new. Entries can be stale; re-check the code before citing one.

## Workflow

1. Scope: by default `git diff`, `git diff --staged` and untracked `.cs` under `Assets/_Core` and `Assets/_Project`. For "review X", everything in X is in scope; tag known-issues items "(pre-existing)".
2. Read whole files and the `_Core` types they use; lifetime bugs are invisible in a hunk.
3. Every finding points at a line you read. Lower the confidence when it depends on runtime, SDK or Inspector state you can't see.
4. Don't edit during a review unless asked. Refactors preserve behaviour and leave third-party folders and `.meta` files alone.
5. Keep `known-issues.md` current: remove entries a change fixes, add pre-existing defects that stay unfixed (format is in the file).

## What tends to go wrong here

**Correctness**
- Code after an `await` or in an SDK/ad callback touching a destroyed component (no `this == null` check); `?.`/`??` on Unity objects or `Singleton.Instance`.
- `async void` outside Unity messages/UI handlers; un-awaited Tasks that swallow exceptions.
- Unpaired `EventBus.Subscribe`, lambda subscriptions, overrides of `GameplayPauseHandler`/`Singleton` missing `base.` calls, `EventBus.Clear()` outside the bootstrapper.
- Singleton access before bootstrap (field initializers, static constructors, `00_Bootstrap` scripts) or in `OnDestroy` during quit without a null check.
- Rewards granted anywhere but the `AdResult.Completed` branch of the rewarded callback; flows that stop on a non-`Completed` interstitial result; flows that wait forever for an ad callback.
- Any write to `Time.timeScale` outside `PauseManager` (use `Pause/Resume(PauseSource)` or `PauseManager.TimeScale`); a `PauseSource` paused and never resumed; input handlers not gated on `PauseManager.IsPaused`.
- New UI prefabs missing from `UIConfig` (or two prefabs with the same root type), scenes missing from Build Settings or `GameScenes`, unassigned `SoundData` fields, pool ids without a `PoolConfig` entry or `CreatePool`; inserted (not appended) values in serialized enums.
- Pooling: double release, use after release, ignoring a null `Get`, position-dependent `OnSpawn` (use the placed `Get`), relying on pooled objects surviving a scene change (they are released on unload).
- Save classes not `JsonUtility`-friendly, renamed fields without migration, saving per frame.

**Architecture**
- `_Core` referencing `_Project`, game content (screens, sounds, scene names) or game rules.
- SDK calls outside `Assets/_Platforms/<Name>/`; bypassing `PlatformManager`/`Capabilities`; a new service without a Null implementation or without registration on every platform.
- Direct `IGameService.GameplayStart/Stop` calls, `BeginGameplay` from `Awake`, Begin/End around ads or pauses, a second audio mute path, click sounds played from handlers instead of `ButtonClickSound`, synchronous `SceneLoader.Load` for player-driven scene changes (use `SceneTransition`).
- `PlatformUser.Id` used as server identity instead of `GetUserToken`.
- Scene names outside `GameScenes`, clip paths, `Find`-style UI lookups or duplicated pool/save-key literals; game tuning in `GameConfig`; runtime state written into ScriptableObjects.
- Mixing 2D and 3D physics types on one object, or code that assumes one dimension inside `_Core`.

**WebGL**
- `Task.Run`, threads, `.Result`, `.Wait()`, `GetResult()`, `Thread.Sleep`, core flows on `Task.Delay`.
- `System.IO` for game data; reflection that managed stripping can break.
- Allocations, LINQ, `Find*`, `GetComponent` or string-building logs in per-frame or per-entity paths; `Instantiate`/`Destroy` for frequent spawns.

**Unity**
- Renamed serialized fields without `[FormerlySerializedAs]`; moved assets without their `.meta`; `UnityEditor` outside `Editor/` or `#if UNITY_EDITOR`; legacy `UnityEngine.Input`; new static state that survives disabled domain reload.

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

Critical = reachable correctness bug, data loss, crash, wrong reward. Major = architecture violation, WebGL breakage, latent bug under a realistic condition. Minor = hot-path cost or maintainability. Nit = style; batch them, skip when there are Critical findings. End with "Pre-existing issues touched by this change" and "Not verified". Write in the user's language; keep identifiers and the template labels as they are.

Useful greps: `async void`, `EventBus\.(Un)?[Ss]ubscribe`, `CrazySDK\.` outside the CrazyGames folder, `using _Project` in `_Core`, `Task\.Run|\.Result\b|\.Wait\(|Thread\.Sleep|Task\.Delay`, `File\.|Directory\.`, `Find(Any|First)ObjectByType|GameObject\.Find`, `Time\.timeScale`, `Instance\?\.`, `Input\.Get`, `SceneManager\.LoadScene`.
