#Requires -Version 7.2
function Assert-Administrator {
    $principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Run this script from an administrator PowerShell 7 terminal, or use Start-CameraTest.ps1 for UAC elevation.'
    }
}
function Save-Session([object]$Session, [string]$Path) {
    $temporary = "$Path.tmp"
    $Session | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $temporary -Encoding utf8
    Move-Item -LiteralPath $temporary -Destination $Path -Force
}
function Restore-CameraServices([object]$Session) {
    $failures = @()
    foreach ($name in @($Session.OriginallyRunning)) {
        try {
            Start-Service -Name $name -ErrorAction Stop
            (Get-Service -Name $name).WaitForStatus('Running',[TimeSpan]::FromSeconds(20))
        } catch { $failures += "${name}: $($_.Exception.Message)" }
    }
    if ($failures.Count) { throw ($failures -join '; ') }
    $Session.ServicesRestored = $true
}
