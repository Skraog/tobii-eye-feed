#Requires -Version 7.2
[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [switch]$Locked)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot 'LibUsbDotNet/src/LibUsbDotNet/LibUsbDotNet.csproj'))) {
    throw 'Initialize the submodule: git submodule update --init --recursive'
}
& (Join-Path $PSScriptRoot 'scripts/Prepare-Native.ps1')
Push-Location $PSScriptRoot
try {
    $buildArgs = @('build','ReVision/ReVision.csproj','-c',$Configuration,'--artifacts-path','.local/artifacts/windows','--nologo')
    if ($Locked) { $buildArgs += '-p:RestoreLockedMode=true' }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    Write-Host 'Run .\Start-CameraTest.ps1, then open http://127.0.0.1:4442/.'
} finally { Pop-Location }
