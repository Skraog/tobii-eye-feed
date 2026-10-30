#Requires -Version 7.2
$ErrorActionPreference = 'Stop'
$sessionPath = Join-Path $PSScriptRoot 'logs/camera-session.json'
if (!(Test-Path -LiteralPath $sessionPath)) { Write-Host 'No camera session is recorded.'; return }
$session = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
$process = Get-Process -Id $session.ProcessId -ErrorAction SilentlyContinue
if (!$process -or $process.StartTime.ToUniversalTime().Ticks -ne $session.StartTicks) { Write-Host 'The camera session has finished.'; return }
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'logs/camera.stop') -Value 'stop' -Encoding utf8
if (!$process.WaitForExit(10000)) { throw 'Shutdown is still pending. Inspect logs/camera.stderr.log.' }
Write-Host 'Camera stopped.'
