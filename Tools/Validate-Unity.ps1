param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'Client'
$artifacts = Join-Path $repo 'Artifacts'
New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
if (-not (Test-Path -LiteralPath $UnityPath)) { throw "Unity executable not found: $UnityPath" }
$log = Join-Path $artifacts 'unity-validation.log'
$report = Join-Path $artifacts 'phase1-validation.json'
if (Test-Path -LiteralPath $report) { Remove-Item -LiteralPath $report }
$argsList = @('-batchmode', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'FPSPrototype.Editor.Phase1Validation.Run', '-logFile', ('"' + $log + '"'))
# Do not use -quit (the validator exits after Play Mode) or -nographics (screenshots need a renderer).
$process = Start-Process -FilePath $UnityPath -ArgumentList $argsList -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddMinutes(6)
while (-not $process.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $deadline) { $process.Kill(); throw "Unity validation exceeded 6 minutes; inspect $log" }
}
if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode); inspect $log" }
if (-not (Test-Path -LiteralPath $report)) { throw "Unity exited without a validation report; inspect $log" }
$result = Get-Content -Raw -LiteralPath $report | ConvertFrom-Json
if (-not $result.passed) { throw "Unity validation failed: $($result.error)" }
$result | ConvertTo-Json
