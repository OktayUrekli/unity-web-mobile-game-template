# CLAUDE.md

Reusable Unity 6 starter template for **2D and 3D** games (exact version in `ProjectSettings/ProjectVersion.txt`; URP, Input System, DOTween). Ships to **CrazyGames (WebGL)** today; Google Play and Yandex Games are stubbed and fall back to `NullPlatform`. `README.md` (Turkish) is the human-facing overview.

The repo ships in the 2D profile (`GAME_2D`, `URP_2D`). Switch with `Tools/Template/Set Up as 2D Game` / `Set Up as 3D Game` and select the platform with `Tools/Template/Platform/...`, never by hand: these tools keep pipelines, defines, physics engines, packages and WebGL templates consistent.

## Working with me

- Reply to the user in Turkish; give Editor steps in Turkish, numbered. Code, comments, skills and CLAUDE.md stay in English.
- File and folder names use ASCII only (no Turkish characters).
- README and other user-facing texts: natural, plain Turkish that doesn't read as AI-written.
- When unsure about a destructive or wide change (deleting assets, editing scenes by hand, touching `_Core`), ask first.
- Git: work on a branch in this checkout. No git worktrees: Unity keeps one Editor and one `Library/` per folder. Commit messages are one plain imperative sentence, like the history (no `feat:` prefix).

## Run and verify

- Play from `Assets/_Project/Scenes/Bootstrap/00_Bootstrap.unity` (build index 0) or a scene with a `BootstrapGuard`. A new playable scene needs one; without it every manager `Instance` is null.
- Check every C# change with `unity-verify`. Without Unity (cloud sessions) report "compile NOT VERIFIED", never "it compiles".
- Automated tests are EditMode tests of plain logic (`_Core`, plus a game's own test assembly if it has one). Gameplay, UI, scenes and prefabs are checked by a compile plus Play Mode steps for the user.
- Builds, size and Build Profiles: `webgl-performance`. CrazyGames release: `/crazygames-release`.

## Rules that are easy to get wrong

- `_Core` (+ `_Core.Editor`) is project-agnostic and holds no game content; game code goes in `_Project` (+ `_Project.Editor`), SDK bridges in `Assets/_Platforms/<PlatformType>`. Namespaces mirror folders. Inside these asmdefs an `Editor/` folder is not special.
- Don't modify `Assets/CrazySDK/`, `Assets/ThirdParty/`, `Assets/Plugins/`, except the template's `CrazyGames.SDK*.asmdef` files and `crazySDK.jslib.meta` (`platform-integration`).
- Game code reaches SDKs only through `PlatformManager.Instance.<Service>` and checks `Capabilities` first. Every service is non-null.
- A level or round is reported with `GameplayStateManager.BeginGameplay()/EndGameplay()`, never `IGameService.GameplayStart/Stop`.
- `PauseManager` owns `Time.timeScale`: `Pause/Resume(PauseSource)`, slow motion with `PauseManager.TimeScale`. A direct write is undone next frame; a game that stays frozen still has a `PauseSource` held.
- Game content plugs in without touching `_Core`: UI by type (`UIManager.ShowPopup<T>()`, prefab in `UIConfig`), sounds as `SoundData` assets, scenes by name (`GameScenes` + Build Settings), pools by string id.
- Renaming, removing or reinterpreting a `SaveData` field raises its `CurrentVersion` and converts old data in `Migrate(fromVersion)`. Adding a defaulted field needs neither.
- UI text never hardcodes a language: the key goes in a Localization string table (`UI` for `_Project`, `Core` for `_Core`) in every Locale.
- Async uses `System.Threading.Tasks`, not coroutines or UniTask. WebGL has no threads: no `Task.Run`, `.Result`, `.Wait()`.
- Fields: `[SerializeField] private camelCase`, private `_camelCase`; XML `<summary>` on public members.
- The template's structure is required, not over-engineering: service interface + Null implementation + registration on every platform, `SaveData` versions, localization keys.
- Active Input Handling stays on **Both** by decision; ignore the Input Manager deprecation warning.

## Skills and plugins

Project skills in `.claude/skills/` hold this project's contracts and traps; read the one that fits and use your own judgement beyond it. They and this file win over any plugin skill, hook or style rule on the same topic; other plugin guidance (terse chat, thinking before coding, small diffs) applies on top.

- Review: `unity-code-review`. Verification: `unity-verify`. New game: `/new-game-from-template`, never a plugin's new-project flow. Commits follow the git rule above, not a plugin's format.
- A plugin skill that runs C# in the Editor through `unity-cli` runs it through Unity MCP (`Unity_RunCommand`) here. Never install `unity-cli` or `com.unity.pipeline`; never add LevelPlay or Unity Gaming Services.
- Sprite atlases are hand-made `.spriteatlasv2` assets; don't add generated atlas scripts.
- A terse style applies to chat only; files, comments, commits and PR text use full sentences.
- No plugin setup unless asked: no status-line offers, no init command that writes other tools' rule files (`AGENTS.md`, `.cursor/`, ...) into the repo.

## Keeping these docs small

Every game made from the template carries this file and the skills, and `/template-sync` compares them.

- Docs hold rules and traps the code doesn't show; signatures, call order and file lists come from the code. Each rule lives in one place; elsewhere, point to it.
- Fix a doc in the same change that makes it wrong. Add a rule only for a trap that would mislead the next session and that the code can't show; prefer fixing the cause (code, validator, tool). No one-off mistakes, history, dates, counts or changelogs.
- A new line is a reason to cut another. CLAUDE.md stays under about 6 KB; a SKILL.md stays short, with rare detail in `references/`.
- Edit docs by hand, never with a compress or trim tool, and name every doc change in your reply.
- Framework skills name no game; a game's own rules go in CLAUDE.md `## Game` or a game skill.
- Current defects: `.claude/skills/unity-code-review/references/known-issues.md` (its header says when to change it).
