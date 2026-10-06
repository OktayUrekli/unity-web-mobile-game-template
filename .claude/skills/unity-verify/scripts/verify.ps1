<#
.SYNOPSIS
    Compiles the Unity project in batch mode (and optionally runs EditMode tests) without the Editor UI.

.DESCRIPTION
    Exit codes:
      0 = success (no compile errors; all tests passed when -Tests is used)
      1 = compile errors or test failures (details printed)
      2 = environment problem (Editor not found, project already open, timeout, licensing, ...)

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .claude/skills/unity-verify/scripts/verify.ps1
    powershell -ExecutionPolicy Bypass -File .claude/skills/unity-verify/scripts/verify.ps1 -Tests
    powershell -ExecutionPolicy Bypass -File .claude/skills/unity-verify/scripts/verify.ps1 -DryRun
#>
[CmdletBinding()]
param(
    # Run EditMode tests after compiling (uses -runTests instead of -quit).
    [switch]$Tests,

    # Resolve paths and run the environment checks only; do not launch Unity.
    [switch]$DryRun,

    # Explicit Unity.exe path. Falls back to $env:UNITY_EDITOR_PATH, then the Hub install for ProjectVersion.txt.
    [string]$UnityPath,

    # Project root. Defaults to the first parent folder of this script that contains ProjectSettings/ProjectVersion.txt.
    [string]$ProjectPath,

    # Seconds before the batch-mode process is stopped. A warm Library compiles in a few minutes,
    # but a cold or deleted Library triggers a full asset import that can take far longer.
    [int]$TimeoutSeconds = 1800,

    # Maximum number of distinct compile errors to print.
    [int]$MaxErrors = 30
)

$ErrorActionPreference = 'Stop'

$EXIT_OK = 0
$EXIT_FAILED = 1
$EXIT_ENV = 2

$HUB_EDITOR_ROOT = 'C:\Program Files\Unity\Hub\Editor'
$LOG_DIR = Join-Path $env:TEMP 'unity-verify'

function Fail-Env([string]$message) {
    Write-Output "ENV ERROR: $message"
    exit $EXIT_ENV
}

# ---------- Resolve project root ----------
if (-not $ProjectPath) {
    $dir = $PSScriptRoot
    while ($dir -and -not (Test-Path (Join-Path $dir 'ProjectSettings\ProjectVersion.txt'))) {
        $dir = Split-Path $dir -Parent
    }
    if (-not $dir) { Fail-Env "Could not find ProjectSettings/ProjectVersion.txt above $PSScriptRoot. Pass -ProjectPath." }
    $ProjectPath = $dir
}
$ProjectPath = (Resolve-Path $ProjectPath).Path.TrimEnd('\')
$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path $versionFile)) { Fail-Env "Not a Unity project (missing $versionFile)." }

# ---------- Resolve Unity.exe ----------
$versionLine = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
if (-not $versionLine) { Fail-Env "m_EditorVersion not found in $versionFile." }
$editorVersion = $versionLine.Matches[0].Groups[1].Value

if (-not $UnityPath) { $UnityPath = $env:UNITY_EDITOR_PATH }
if (-not $UnityPath) { $UnityPath = Join-Path $HUB_EDITOR_ROOT "$editorVersion\Editor\Unity.exe" }
if (-not (Test-Path $UnityPath)) {
    Fail-Env "Unity $editorVersion not found at '$UnityPath'. Install it via Unity Hub, or pass -UnityPath / set UNITY_EDITOR_PATH."
}

# ---------- Detect an Editor that already has this project open ----------
$lockFile = Join-Path $ProjectPath 'Temp\UnityLockfile'
if (Test-Path $lockFile) {
    try {
        $stream = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $stream.Close()
    } catch {
        Fail-Env "The project is open in the Unity Editor (Temp/UnityLockfile is locked). Ask the user to close the Editor, then rerun."
    }
}

$normalizedProject = $ProjectPath.Replace('/', '\').ToLowerInvariant()
$unityProcs = @(Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" -ErrorAction SilentlyContinue)
foreach ($p in $unityProcs) {
    if ($p.CommandLine -and $p.CommandLine.Replace('/', '\').ToLowerInvariant().Contains($normalizedProject)) {
        Fail-Env "A Unity process (PID $($p.ProcessId)) already has this project open. Ask the user to close the Editor, then rerun."
    }
}

# ---------- Build arguments ----------
New-Item -ItemType Directory -Force -Path $LOG_DIR | Out-Null
$mode = if ($Tests) { 'editmode' } else { 'compile' }
$logFile = Join-Path $LOG_DIR "$mode.log"
$resultsFile = Join-Path $LOG_DIR 'editmode-results.xml'
foreach ($f in @($logFile, $resultsFile)) { if (Test-Path $f) { Remove-Item $f -Force } }

$unityArgs = @('-batchmode', '-nographics', '-projectPath', "`"$ProjectPath`"", '-logFile', "`"$logFile`"")
if ($Tests) {
    # -quit must not be combined with -runTests; the test runner exits by itself.
    $unityArgs += @('-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$resultsFile`"")
} else {
    $unityArgs += '-quit'
}

Write-Output "Unity $editorVersion | mode: $mode | timeout: ${TimeoutSeconds}s"
Write-Output "Log: $logFile"
if ($DryRun) {
    Write-Output "DRY RUN: environment OK. Would run:"
    Write-Output "  `"$UnityPath`" $($unityArgs -join ' ')"
    exit $EXIT_OK
}

# ---------- Run ----------
$started = Get-Date
$proc = Start-Process -FilePath $UnityPath -ArgumentList $unityArgs -PassThru -WindowStyle Hidden
if (-not $proc.WaitForExit($TimeoutSeconds * 1000)) {
    # Only the batch-mode process started by this script is stopped.
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Fail-Env "Timed out after ${TimeoutSeconds}s (a first import can be slow; rerun with a larger -TimeoutSeconds). Log: $logFile"
}
$unityExit = $proc.ExitCode
$elapsed = [int]((Get-Date) - $started).TotalSeconds
Write-Output "Unity exited with code $unityExit after ${elapsed}s"

if (-not (Test-Path $logFile)) { Fail-Env "Unity produced no log at $logFile." }
$log = Get-Content $logFile

# ---------- Environment failures that are not code problems ----------
$envPatterns = @(
    'another Unity instance is running with this project open',
    'No valid Unity Editor license',
    'License is not active',
    'Failed to activate/update license'
)
foreach ($pattern in $envPatterns) {
    $hit = $log | Select-String -SimpleMatch $pattern | Select-Object -First 1
    if ($hit) { Fail-Env "$($hit.Line.Trim()) (see $logFile)" }
}

# ---------- Compile errors ----------
$errorRegex = '^(?<loc>.+?\(\d+,\d+\)): error (?<code>CS\d+): (?<msg>.+)$'
$errors = @($log | Where-Object { $_ -match $errorRegex } | ForEach-Object { $_.Trim() } | Sort-Object -Unique)
$warningCount = @($log | Where-Object { $_ -match 'warning CS\d+' } | Sort-Object -Unique).Count

if ($errors.Count -gt 0) {
    Write-Output "COMPILE FAILED: $($errors.Count) distinct error(s), $warningCount warning(s)"
    $errors | Select-Object -First $MaxErrors | ForEach-Object { Write-Output "  $_" }
    if ($errors.Count -gt $MaxErrors) { Write-Output "  ... $($errors.Count - $MaxErrors) more in the log" }
    exit $EXIT_FAILED
}

$compilerFailed = $log | Select-String -SimpleMatch 'Scripts have compiler errors' | Select-Object -First 1
if ($compilerFailed) {
    Write-Output "COMPILE FAILED: Unity reported compiler errors but no 'error CSxxxx' line was parsed. Search the log for 'error'."
    exit $EXIT_FAILED
}

Write-Output "Compile OK ($warningCount distinct warning(s))"

# ---------- Tests ----------
if ($Tests) {
    if (-not (Test-Path $resultsFile)) {
        Fail-Env "No test results written (Unity exit code $unityExit). Check the log: $logFile"
    }
    [xml]$xml = Get-Content $resultsFile -Raw
    $run = $xml.'test-run'
    Write-Output "Tests: total $($run.total), passed $($run.passed), failed $($run.failed), skipped $($run.skipped)"
    if ([int]$run.total -eq 0) { Write-Output "Note: no EditMode tests exist in this project." }

    $failedCases = @($xml.SelectNodes("//test-case[@result='Failed']"))
    if ($failedCases.Count -gt 0) {
        foreach ($case in $failedCases) {
            $message = ''
            $messageNode = $case.SelectSingleNode('failure/message')
            if ($messageNode) { $message = ($messageNode.InnerText.Trim() -split "`n")[0].Trim() }
            Write-Output "  FAILED $($case.fullname): $message"
        }
        exit $EXIT_FAILED
    }
    if ([int]$run.failed -gt 0) { exit $EXIT_FAILED }
    Write-Output "TESTS PASSED"
    exit $EXIT_OK
}

if ($unityExit -ne 0) {
    Fail-Env "Unity exited with code $unityExit without compile errors in the log. Check: $logFile"
}
Write-Output "VERIFY PASSED"
exit $EXIT_OK
