#Requires -Version 7.2
[CmdletBinding()]
param([ValidateRange(0,86400)][int]$PreviewSeconds = 0, [ValidateRange(1024,65535)][int]$Port = 4442)
$ErrorActionPreference = 'Stop'
if (!$IsWindows) { throw 'Use run-linux.sh on Linux.' }
$exe = Join-Path $PSScriptRoot '.local/artifacts/windows/bin/ReVision/release/ReVision.exe'
if (!(Test-Path -LiteralPath $exe)) { throw 'Run .\Build.ps1 first.' }
$logRoot = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$stopPath = Join-Path $logRoot 'camera.stop'
$sessionPath = Join-Path $logRoot 'camera-session.json'
$lock = [IO.File]::Open((Join-Path $logRoot 'launch.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
try {
    if (Test-Path -LiteralPath $sessionPath) {
        $previous = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
        $running = Get-Process -Id $previous.ProcessId -ErrorAction SilentlyContinue
        if ($running -and $running.StartTime.ToUniversalTime().Ticks -eq $previous.StartTicks) {
            throw "A camera session is already running. Open http://127.0.0.1:$($previous.Port)/ or run Stop-CameraTest.ps1."
        }
    }
    if (Test-Path -LiteralPath $stopPath) { Remove-Item -LiteralPath $stopPath }
    $arguments = '--port {0} --stop-file "{1}"' -f $Port,$stopPath
    if ($PreviewSeconds -gt 0) { $arguments += " --duration $PreviewSeconds" }
    $process = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot 'camera.stdout.log') -RedirectStandardError (Join-Path $logRoot 'camera.stderr.log')
    [ordered]@{ProcessId=$process.Id;StartTicks=$process.StartTime.ToUniversalTime().Ticks;Port=$Port} | ConvertTo-Json | Set-Content -LiteralPath $sessionPath -Encoding utf8
} finally { $lock.Dispose() }
Write-Host "Camera PID: $($process.Id). Dashboard: http://127.0.0.1:$Port/"
if ($PreviewSeconds -eq 0) { Write-Host 'Continuous session; no time limit.' }
$competing = @(Get-Service -Name 'SRanipalService','Tobii VRU02 Runtime' -ErrorAction SilentlyContinue | Where-Object Status -eq Running)
if ($competing.Count) { Write-Host 'SRanipal/Tobii services are running. If capture is blocked, close their camera access; see docs/troubleshooting.md.' }
Write-Host 'Stop with .\Stop-CameraTest.ps1. Logs: logs/camera.stdout.log and logs/camera.stderr.log.'
