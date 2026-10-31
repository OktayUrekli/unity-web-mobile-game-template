---
name: template-sync
description: Compares a game's template-owned files (Assets/_Core, the platform bridges and WebGL templates, BuildValidator, the CrazySDK asmdefs, packages, .claude skills and settings, CLAUDE.md) with the template's main branch (read-only shallow clone), sorts every difference into game-specific, game improvement for the template, template newer, or unsure, and reports before changing anything. Invoked manually with /template-sync in a game repository made from the template.
disable-model-invocation: true
---

# Template sync

Runs in a game repository created from the template https://github.com/OktayUrekli/unity-web-mobile-game-template (branch `main`). If `git remote get-url origin` is the template itself, stop.

## 1. Clone the template read-only

```bash
tmp="$(mktemp -d)"
git clone --quiet --depth 1 --branch main --single-branch https://github.com/OktayUrekli/unity-web-mobile-game-template.git "$tmp/template"
chmod -R a-w "$tmp/template"
```

Never edit, commit or push in the clone. If the clone fails (network, the session can't reach the repo), stop and say so. Clean up at the end: `chmod -R u+w "$tmp" && rm -rf "$tmp"`.

## 2. Compare

```bash
diff -rq -x '*.meta' "$tmp/template/Assets/_Core" Assets/_Core
diff -rq -x '*.meta' "$tmp/template/Assets/_Platforms" Assets/_Platforms
diff -rq -x '*.meta' "$tmp/template/Assets/WebGLTemplates" Assets/WebGLTemplates
diff -rq -x '*.meta' "$tmp/template/.claude/skills" .claude/skills
diff -u "$tmp/template/CLAUDE.md" CLAUDE.md
for f in Assets/_Project/Scripts/Editor/BuildValidator.cs Assets/Plugins/crazySDK.jslib.meta \
    $(cd "$tmp/template" && find Assets/CrazySDK -name 'CrazyGames.SDK*.asmdef') Packages/manifest.json .claude/settings.json; do
  diff -q "$tmp/template/$f" "$f"
done
grep -h 'const string Version' "$tmp/template/Assets/CrazySDK/Scripts/CrazySDK.cs" Assets/CrazySDK/Scripts/CrazySDK.cs
```

In CLAUDE.md the intro paragraphs and `## Game` belong to the game; compare the other sections. `BuildValidator` is the only `_Project` file compared; game code, scenes and the rest of `_Project` stay out. The 2D/3D profile tool and the game's own packages also change `Packages/manifest.json`, so compare it package by package. A different CrazySDK `Version` goes in the report on its own: bridge changes may depend on it, and updating it means re-importing the SDK (`platform-integration`), not copying files.

Read `diff -u <template file> <game file>` for each differing file. The clone has no history, so judge direction from the game's side:
- `git log --format='%h %ad %s' --date=short -- <path>`: only the game's first commit means the game never touched the file and the template moved on; later commits mean the game changed it.
- A file only in the template: `git log --diff-filter=D --oneline -- <path>` shows whether the game deleted it on purpose.

## 3. Classify every difference

| Class | Typical signs |
|---|---|
| game-specific (leave) | the game's own skill or text, including what `/new-game-from-template` changed (CLAUDE.md intro and `## Game`, the known-issues `Gameplay` section); a template file the game deleted on purpose |
| game improvement (move to template) | a game commit changed template-owned code or a skill rule in a way that holds for every game: a bug fix, a generic API, a new pitfall |
| template newer (move to game) | the game never touched the file and the template's copy differs; a file only in the template that the game did not delete |
| unsure | both sides changed the same file, or the intent is unclear |

`_Core` holds no game content; game content found there is worth reporting on its own.

## 4. Report, then wait

Group the report by class: path, one line on what differs, and for "unsure" the question the user has to answer. Change no file until the user says which differences move in which direction.

## 5. Carry over what the user approved

- Template to game: copy the template's file over the game's; for CLAUDE.md replace only the approved sections. A file new to the game brings its template `.meta` (same GUID, so references and later syncs stay consistent); an existing file keeps the game's `.meta`. After a code, asmdef or package change run `unity-verify` (or report "compile NOT VERIFIED" per its no-Unity rule) and update `known-issues.md` when the change fixes or alters a listed defect.
- Game to template: leave the clone alone. Save the `diff -u` of each approved file to a patch outside the repo and tell the user to apply it in a session on the template repo, where that repo's own git rules apply.
