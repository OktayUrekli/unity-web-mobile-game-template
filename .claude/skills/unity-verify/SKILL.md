---
name: unity-verify
description: Verifies that this Unity project's C# code compiles, and optionally that EditMode tests pass - through the connected Unity MCP when the Editor is open, otherwise by running the Editor in batch mode (scripts/verify.ps1). Use after any change to a .cs file, .asmdef, package manifest or hand-edited asset, before reporting a task as done, when the user asks "does it compile", "check for errors", "run the tests", "verify my changes", or when a compile error or CS#### code is mentioned.
---

# Unity verify (batch-mode compile and EditMode tests)

Prove a C# change compiles before saying it is finished. Pick the path by Editor state:

- **Editor open + Unity MCP connected** (`mcp__unity-mcp__*` tools available) → MCP path below.
  The batch script cannot run while the Editor has the project open (it exits 2).
- **Editor closed** → batch script (`scripts/verify.ps1`).

Don't use the plugin's `unity-cli` or install `com.unity.pipeline` for this. Never report "done"
with a failing or skipped verify.

## MCP path (Editor open)

1. `mcp__unity-mcp__Unity_RunCommand` with `AssetDatabase.Refresh();` so the Editor imports the
   changed files (it may not auto-refresh while unfocused).
2. The refresh triggers a recompile and domain reload; during it MCP calls fail with
   "Unity not detected". That is expected: retry the next call rather than treating it as an error.
3. Read state with another `Unity_RunCommand` logging `EditorApplication.isCompiling` and
   `EditorUtility.scriptCompilationFailed`. If still compiling, retry.
4. `mcp__unity-mcp__Unity_GetConsoleLogs` with `logTypes: "Error"` lists
   `file(line,col): error CSxxxx: msg`. Fix, then repeat from step 1 until
   `scriptCompilationFailed=False` and no errors.

`Unity_RunCommand` still executes while the project has compile errors (it compiles its own
script separately), so this loop works on a broken project.

EditMode tests with the Editor open: run them through `TestRunnerApi` from `Unity_RunCommand`, with
a callback class next to `CommandScript` that logs the summary (the MCP rejects `System.IO` and
reflection, so log instead of writing a file):

```csharp
internal class TestResultLogger : ICallbacks   // UnityEditor.TestTools.TestRunner.Api
{
    public void RunFinished(ITestResultAdaptor r) =>
        Debug.Log($"EDITMODE_RESULTS PASS={r.PassCount} FAIL={r.FailCount} SKIP={r.SkipCount}");
    // also log FullName + Message of each failed leaf; RunStarted/TestStarted/TestFinished empty
}
// in Execute: var api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new TestResultLogger());
// api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode,
//     assemblyNames = new[] { "_Core.Tests.Editor" } }) { runSynchronously = true });
```

The call reports "executed partially" because tests log expected errors; that is not a failure.
Read the result with `grep EDITMODE_RESULTS "%LOCALAPPDATA%/Unity/Editor/Editor.log"` (the last
match). A test that sets `Time.timeScale` can make Unity rewrite `ProjectSettings/TimeManager.asset`
in a newer format; restore it with `git checkout` if it shows up in the diff.

## Batch path (Editor closed)

Run from the project root (the folder containing `ProjectSettings/`):

```
powershell -ExecutionPolicy Bypass -File .claude/skills/unity-verify/scripts/verify.ps1
powershell -ExecutionPolicy Bypass -File .claude/skills/unity-verify/scripts/verify.ps1 -Tests
```

- Default: compile-only (`-batchmode -nographics -quit`).
- `-Tests`: compile and run EditMode tests (`-runTests -testPlatform EditMode`): the project's
  own tests in `Assets/_Core/Tests/Editor` plus package tests the runner picks up (e.g. an
  Addressables stub), so the total is a little higher than the project's count. Failed tests are listed as `FAILED <full name>: <message>`.
- `-DryRun`: only resolve paths and run the environment checks (fast; use it to check whether the
  Editor is open before a long run).
- `-TimeoutSeconds <n>` (default 1800), `-UnityPath <exe>` or env `UNITY_EDITOR_PATH` to override
  the Editor resolved from `ProjectSettings/ProjectVersion.txt`.

Duration: a warm `Library/` usually takes a few minutes (Unity starts, imports changes, compiles).
If `Library/` is missing or stale, the first run imports every asset and can take much longer.
Because the Bash/PowerShell tool timeout is 10 minutes, run it with `run_in_background: true`
(or a 600000 ms timeout) and wait for the completion notification instead of polling.

## Exit codes and output

| Exit | Meaning | What to do |
|---|---|---|
| 0 | `VERIFY PASSED` / `TESTS PASSED` | Report success (and what was not verified, below) |
| 1 | `COMPILE FAILED` (list of `file(line,col): error CSxxxx: msg`) or failed tests | Fix and rerun |
| 2 | `ENV ERROR: ...` (Editor open, Unity not installed, license, timeout) | Tell the user; do not "fix" code |

Output is kept short on purpose. The full Unity log path is printed (`%TEMP%\unity-verify\`);
open it only when the summary is not enough (e.g. exit 2 with an unknown cause, or
"compiler errors but no CS line parsed").

## The Editor must be closed

Unity allows only one Editor per project. If the user has the project open, the script exits 2
with "project is open in the Unity Editor". Never kill Unity processes yourself; ask the user to save and close the Editor, then rerun.

If the user prefers to keep the Editor open, ask them to check the Console after Unity recompiles
and paste any red errors; treat that as the verify result.

## What this does not verify

A clean compile says nothing about runtime behaviour. Always tell the user which of these they
still need to check in Play Mode (starting from `00_Bootstrap`):

- Play Mode behaviour, null references at runtime, async/bootstrap ordering.
- Scene and prefab wiring: missing serialized references, "missing script" components,
  config asset entries (UIConfig prefabs, SceneConfig, AudioConfig, PoolConfig), unassigned `SoundData` fields.
- WebGL/CrazyGames-only behaviour (ads, SDK, saves); see the `webgl-performance` skill.
- Code under `#if UNITY_WEBGL && !UNITY_EDITOR` or other platform defines is compiled for the
  Editor's active build target only; other targets are not checked.
