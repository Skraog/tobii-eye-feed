#Requires -Version 7.2
# Direct console mode. See docs/troubleshooting.md if another application owns the camera.
$ErrorActionPreference = 'Stop'
$dll = Join-Path $PSScriptRoot '.local/artifacts/windows/bin/ReVision/release/ReVision.dll'
if (!(Test-Path -LiteralPath $dll)) { throw 'Run .\Build.ps1 first.' }
& dotnet $dll @args
exit $LASTEXITCODE
