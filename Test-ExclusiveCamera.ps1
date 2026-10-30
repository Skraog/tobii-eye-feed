#Requires -Version 7.2
[CmdletBinding()]
param(
    [ValidateRange(0,86400)][int]$PreviewSeconds = 0,
    [ValidateRange(1024,65535)][int]$Port = 4442,
    [switch]$NoHardware
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/CameraServices.ps1')
$logRoot = Join-Path $PSScriptRoot 'logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$summaryPath = Join-Path $logRoot 'exclusive-camera-result.json'
$stopPath = Join-Path $logRoot 'camera-test.stop'
$childStop = Join-Path $logRoot 'camera-child.stop'
$sessionLock = $null
$child = $null
$session = $null
try {
    # File sharing is disabled for the lifetime of the supervisor, including service restoration.
    $sessionLock = [IO.File]::Open((Join-Path $logRoot 'camera-session.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if (Test-Path -LiteralPath $summaryPath) {
        $previous = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
        if (!$previous.ServicesRestored -and @($previous.OriginallyRunning).Count) {
            throw 'An interrupted session has pending service recovery. Run .\Restore-Services.ps1 as administrator first.'
        }
    }
    if (!$NoHardware) { Assert-Administrator }
    $exe = Join-Path $PSScriptRoot '.local/artifacts/windows/bin/ReVision/release/ReVision.exe'
    if (!(Test-Path -LiteralPath $exe)) { throw 'Run .\Build.ps1 first.' }
    if (Get-Process -Name ReVision -ErrorAction SilentlyContinue) { throw 'Another ReVision process is running. Stop that session first.' }
    $original = @()
    if (!$NoHardware) {
        foreach ($name in @('SRanipalService','Tobii VRU02 Runtime')) {
            $service = Get-Service -Name $name -ErrorAction SilentlyContinue
            if ($service -and $service.Status -eq 'Running') { $original += $name }
        }
    }
    $session = [ordered]@{ Started=(Get-Date -Format o); Finished=$null; OriginallyRunning=$original; ServicesRestored=$false;
        ProcessId=$null; Port=$Port; Mode=$(if ($PreviewSeconds) {'Timed'} else {'Continuous'}); Error=$null; StopReason=$null; Devices=@() }
    # Persist original state before changing any service, so recovery also works after a supervisor crash.
    Save-Session $session $summaryPath
    foreach ($path in @($stopPath,$childStop)) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
    foreach ($name in $original) {
        Stop-Service -Name $name -ErrorAction Stop
        (Get-Service -Name $name).WaitForStatus('Stopped',[TimeSpan]::FromSeconds(20))
    }
    $arguments = '--port {0} --stop-file "{1}"' -f $Port,$childStop
    if ($NoHardware) { $arguments += ' --no-hardware' }
    $child = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $exe) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logRoot 'exclusive-camera.stdout.log') -RedirectStandardError (Join-Path $logRoot 'exclusive-camera.stderr.log')
    $session.ProcessId = $child.Id
    Save-Session $session $summaryPath
    $deadline = if ($PreviewSeconds -gt 0) { (Get-Date).AddSeconds($PreviewSeconds) } else { $null }
    while ($true) {
        if (Test-Path -LiteralPath $stopPath) { $session.StopReason = 'StopRequested'; break }
        if ($child.HasExited) { throw "ReVision exited with code $($child.ExitCode). See logs/exclusive-camera.stderr.log." }
        if ($deadline -and (Get-Date) -ge $deadline) { $session.StopReason = 'PreviewExpired'; break }
        try { $session.Devices = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/devices" -TimeoutSec 2 }
        catch { } # USB reconnection and temporary HTTP failures do not end an unlimited session.
        Save-Session $session $summaryPath
        Start-Sleep -Seconds 1
    }
} catch {
    if ($session) { $session.Error = $_.Exception.Message; $session.StopReason = 'Error' }
    else { Write-Error $_ -ErrorAction Continue }
} finally {
    if ($session) {
        try {
            if ($child -and !$child.HasExited) {
                Set-Content -LiteralPath $childStop -Value 'stop' -Encoding utf8
                if (!$child.WaitForExit(10000)) {
                    $session.Error = "$($session.Error) Graceful shutdown exceeded ten seconds; the child was terminated.".Trim()
                    Stop-Process -InputObject $child -Force
                    $child.WaitForExit()
                }
            }
            Restore-CameraServices $session
        } catch { $session.Error = "$($session.Error) $($_.Exception.Message)".Trim() }
        $session.Finished = Get-Date -Format o
        Save-Session $session $summaryPath
    }
    if ($sessionLock) { $sessionLock.Dispose() }
}
if ($session -and $session.Error) { Write-Error $session.Error; exit 1 }
