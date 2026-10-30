#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/CameraServices.ps1')
Assert-Administrator
$summaryPath = Join-Path $PSScriptRoot 'logs/exclusive-camera-result.json'
if (!(Test-Path -LiteralPath $summaryPath)) { Write-Host 'No service recovery is recorded.'; return }
$sessionLock = [IO.File]::Open((Join-Path $PSScriptRoot 'logs/camera-session.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    $session = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
    if ($session.ServicesRestored) { Write-Host 'Services have already been restored.'; return }
    if (Get-Process -Name ReVision -ErrorAction SilentlyContinue) { throw 'Stop ReVision before restoring competing services.' }
    Restore-CameraServices $session
    $session.Finished = Get-Date -Format o
    Save-Session $session $summaryPath
    Write-Host 'Original service states restored.'
} finally { $sessionLock.Dispose() }
