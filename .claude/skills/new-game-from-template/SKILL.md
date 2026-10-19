---
name: new-game-from-template
description: Turns a fresh game repository created from this template into the game's own project - Editor setup steps for the user (New Game Setup, 2D/3D profile, platform), removing the template's test scenes and harnesses, adapting CLAUDE.md, skills, known issues and README to the game, ending with a checklist report. Invoked manually with /new-game-from-template.
disable-model-invocation: true
argument-hint: "[game name]"
---

# New game from the template

Game name: `$ARGUMENTS`. If it is empty, or the one-line pitch and 2D/3D choice are unknown, ask for them first. Run this once, in a new repository created from the template, before writing gameplay. If `git remote get-url origin` points at `unity-web-mobile-game-template`, stop: that is the template itself.

Steps b-g don't wait for step a; do them while the user works in the Editor. Ask before any deletion.

Copy this checklist into your reply and tick items as you go:

```
New game checklist
- [ ] a. User: Editor setup (New Game Setup, profile, platform)
- [ ] b. Remove the template's test scenes and harnesses
- [ ] c. Adapt CLAUDE.md to the game
- [ ] d. Fix skill references to the removed harnesses
- [ ] e. Prune known-issues.md, add a Gameplay section
- [ ] f. gameplay-feature: session owner paragraph
- [ ] g. README.md for the game
```

## a. Editor setup (user)

Give these to the user in Turkish, numbered, and wait for their result:

1. Open the project in Unity 6000.3.21f1 (the first import takes a while).
2. `Tools/Template/New Game Setup`: company, product name (the game name), bundle id, version, orientation and its UI scaling, optional icon and loading logo; apply.
3. `Tools/Template/Set Up as 2D Game` or `Set Up as 3D Game` to match the game (the template ships 2D). Wait for the recompile.
4. `Tools/Template/Platform/...`: the first target platform (`CrazyGames (WebGL)` today).
5. Save, play from `00_Bootstrap`, check that the Console shows no red errors, and commit the changed settings.

## b. Remove the template's test scenes and harnesses

Targets, each with its `.meta`: `Assets/_Project/Scenes/Test/99_Test.unity` and `99_Test3D.unity`, everything under `Assets/_Project/Testing/` (scripts, `3D/_Project.Testing3D.asmdef`, `3D/Materials/*.mat`) and under `Assets/_Platforms/CrazyGames/Testing/`. Follow `unity-asset-editing`.

1. List them: `find Assets/_Project/Scenes/Test Assets/_Project/Testing Assets/_Platforms/CrazyGames/Testing -type f ! -name '*.meta'`. Files the game added there are not targets.
2. For every script and material: `bash .claude/skills/unity-asset-editing/scripts/find-guid.sh --refs <path>`. Expected hits: only `99_Test.unity` / `99_Test3D.unity`. For the asmdef: `grep -rl "_Project.Testing3D\|GUID:<its guid>" Assets Packages --include=*.asmdef --include=*.asmref` finds nothing. Any other hit: keep that file and report it.
3. Show the user the list and the reference results. Delete only after an explicit yes.
4. Delete each file together with its `.meta`: with the Editor open and Unity MCP connected, `AssetDatabase.DeleteAsset(path)` (test scenes closed first); otherwise `git rm -r <folder> <folder>.meta` for a folder holding only template files, `git rm <file> <file>.meta` for single files. Remove the `.meta` of every folder left empty (`Assets/_Project/Testing.meta`, `Assets/_Project/Scenes/Test.meta`, `Assets/_Platforms/CrazyGames/Testing.meta`): git keeps no empty folders and Unity warns about a folder `.meta` without its folder.
5. Check: `grep -n "Scenes/Test" ProjectSettings/EditorBuildSettings.asset` finds nothing, `git status` shows each deleted file with its `.meta`, then run `unity-verify` (scripts and an asmdef are gone).

Leave `BuildValidator` and `crazygames-release/scripts/preflight.sh` as they are: they keep checking those folders, so the game's own harnesses stay out of builds.

## c. Adapt CLAUDE.md

- First paragraph: introduce the game (name, one-line pitch, 2D or 3D, target platforms) and keep the tech line (Unity version, URP, Input System, DOTween). It is no longer a "reusable starter template". If the game is 3D, fix the profile paragraph ("ships in the 2D profile").
- In the EditMode tests bullet of "Run and verify", replace the text from "`Assets/_Project/Testing/*` and `Assets/_Platforms/CrazyGames/Testing/*` are keyboard-driven harnesses" through "Test scenes stay out of Build Settings." with:
  > A new manual harness goes in `Assets/_Project/Testing/` inside a scene under `Scenes/Test/`; test scenes stay out of Build Settings.
- Add `## Game` right after `## Working with me`, 6-8 lines at most: the core of the game, a pointer to the design in `README.md`, and the game's own easy-to-get-wrong rules. Longer game detail goes in a game project skill (`.claude/skills/<game-slug>/SKILL.md`, with a description naming the game's systems), not in CLAUDE.md.

## d. Fix skill references to the removed harnesses

Make exactly these replacements (paths under `.claude/skills/`):

- `core-systems/SKILL.md`, `## Verify`: replace "Harness style: `Assets/_Project/Testing/SaveTest.cs` in `99_Test`." with:
  > Manual harnesses (none ship with this game) go in `Assets/_Project/Testing/` inside a scene under `Scenes/Test/`.
- `platform-integration/SKILL.md`, `## Verify`: replace the whole paragraph with:
  > Play from `00_Bootstrap` (expect `Platform initialized successfully: <Type>`), tick `alwaysThrowAdError` for the failure path, and set `PlatformConfig` to an unimplemented type to check that the game still runs and saves. Manual harnesses (none ship with this game) go in `Assets/_Project/Testing/` (or `Assets/_Platforms/<Name>/Testing/` when they use SDK types) inside a scene under `Scenes/Test/`.
- `platform-integration/references/new-service-checklist.md`, body of `## 11. Test harness`:
  > Add a keyboard/ContextMenu-driven MonoBehaviour under `Assets/_Project/Testing/` (or `Assets/_Platforms/<Platform>/Testing/` if it uses SDK types) to a scene under `Assets/_Project/Scenes/Test/` that has a `BootstrapGuard` (the user creates the scene in the Editor; never add it to Build Settings), then play that scene.
- `unity-asset-editing/SKILL.md`:
  - "Real example from `Assets/_Project/Scenes/Test/99_Test.unity`:" becomes "Example scene YAML (from the template's test scene, not in this game; fileIDs and GUIDs are illustrative):".
  - In "Finding GUIDs and references", use `Assets/_Core/Bootstrap/BootstrapGuard.cs` instead of `Assets/_Project/Testing/SaveTest.cs` and write the output the script prints now.
  - Replace the paragraph from "Example: removing the `TestHarness` object" through "remove it with the recipes above." with:
    > Manual harnesses (none ship with this game) go in `Assets/_Project/Testing/` inside a scene under `Scenes/Test/`, never in a build scene; if one shows up in a build scene, remove it with the recipes above.
- `crazygames-release/SKILL.md`, "Debug input" row: only once the game changes or replaces `GameplayController`, replace "`GameplayController` Space to score" with the game's own raw keyboard input (`<Type>` <key> to <action>), or drop it and keep only "`UIManager` Back/Escape". Until then leave it and say so in the report note.

Then `grep -rn "99_Test\|SaveTest\|GameplayPauseTest\|CrazyGamesAdsTest\|CrazyGamesUserTest\|TestHarness" CLAUDE.md .claude` should hit only this skill and the illustrative YAML block.

## e. known-issues.md

In `unity-code-review/references/known-issues.md`, check each entry's `path` and type still exist in the game and remove entries for files or objects the game removed (for example the `01_MainMenu` CrazyBanner entry if the game replaced that scene). `_Core` entries stay. Add `## Gameplay` (and its Contents line) for defects in the game's own code, same entry format, starting with "None known yet."

## f. gameplay-feature session owner

The paragraph "The real example is `Assets/_Project/Scripts/Systems/GameplayController.cs` ..." in `gameplay-feature/SKILL.md` describes the template's demo loop. Once the game's session owner is written (or `GameplayController` is rewritten for the game), replace it with: "The session owner is `<path>` in `<scene>` (<what it owns: HUD, pause and game over, saving, ads, `GameplayStateManager`>). Extend it rather than adding a second session owner." If it doesn't exist yet, mark PENDING and remind the user.

## g. README.md

`README.md` is the human-facing overview, in Turkish. Remind the user (or draft it if asked) to rewrite it for the game: name, short pitch, controls, the game's design, and only the template setup steps the game still needs. Drop the "Test sahneleri" section and the `99_Test` / `99_Test3D` and "test araçları" mentions under "Klasör yapısı". Natural, plain Turkish that doesn't read as AI-written.

## Report

End with this report; never mark a step DONE that you did not do and check:

```
New game report: <game name>
a. Editor setup:        DONE | PENDING (user) | SKIPPED - <note>
b. Test scenes/harness: DONE | PENDING (user) | SKIPPED - <note>
c. CLAUDE.md:           DONE | PENDING (user) | SKIPPED - <note>
d. Skill references:    DONE | PENDING (user) | SKIPPED - <note>
e. Known issues:        DONE | PENDING (user) | SKIPPED - <note>
f. Session owner text:  DONE | PENDING (user) | SKIPPED - <note>
g. README:              DONE | PENDING (user) | SKIPPED - <note>
```
