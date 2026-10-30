#Requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('win-x64','linux-x64')][string]$Runtime)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (($Runtime -eq 'win-x64') -ne $IsWindows) { throw 'Package on the target operating system.' }
if ($IsWindows) { & (Join-Path $PSScriptRoot 'Prepare-Native.ps1') }
Push-Location $root
try {
    $target = Join-Path $root "artifacts/tobii-eye-feed-$Runtime"
    if (Test-Path -LiteralPath $target) { throw "Output exists: $target. Choose a clean checkout for packaging." }
    & dotnet publish ReVision/ReVision.csproj -c Release --self-contained false --artifacts-path .local/artifacts/package -p:RestoreLockedMode=true -o $target
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    foreach ($name in @('LICENSE','README.md','THIRD-PARTY-NOTICES.md','CHANGELOG.md','licenses','docs')) {
        Copy-Item -LiteralPath (Join-Path $root $name) -Destination $target -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $root 'LibUsbDotNet/LICENSE') -Destination (Join-Path $target 'licenses/LibUsbDotNet.txt')
    Set-Content -LiteralPath (Join-Path $target 'RUNNING.txt') -Value 'Install the ASP.NET Core 10 runtime. Windows: pwsh -File Run.ps1. Linux: bash run.sh. The dashboard is http://127.0.0.1:4442/. Ctrl+C stops capture. No automatic elevation or service changes. See docs/troubleshooting.md for competing Windows services, and docs/linux.md for Linux native libraries and device permissions.' -Encoding utf8
    if ($IsWindows) {
        Set-Content -LiteralPath (Join-Path $target 'Run.ps1') -Value '& dotnet (Join-Path $PSScriptRoot "ReVision.dll") @args' -Encoding utf8
        Compress-Archive -LiteralPath $target -DestinationPath "$target.zip"
    } else {
        New-Item -ItemType Directory -Path (Join-Path $target 'linux') -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $root 'linux/70-revision-vive-pro-eye.rules') -Destination (Join-Path $target 'linux')
        Set-Content -LiteralPath (Join-Path $target 'run.sh') -Value '#!/usr/bin/env bash', 'set -euo pipefail', 'exec dotnet "$(dirname -- "$0")/ReVision.dll" "$@"' -Encoding utf8
        & tar -czf "$target.tar.gz" -C (Split-Path -Parent $target) (Split-Path -Leaf $target)
        if ($LASTEXITCODE -ne 0) { throw 'Archive failed.' }
    }
} finally { Pop-Location }
