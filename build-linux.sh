#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
command -v dotnet >/dev/null || { printf 'Install .NET 10 SDK first (Arch: sudo pacman -S --needed dotnet-sdk-10.0 aspnet-runtime-10.0).\n' >&2; exit 1; }
if [[ ! -f "$root/LibUsbDotNet/src/LibUsbDotNet/LibUsbDotNet.csproj" ]]; then
    printf 'Missing USB submodule. Run: git submodule update --init --recursive\n' >&2
    exit 1
fi
# Isolate Linux intermediates from a Windows checkout shared through WSL.
cd "$root"
dotnet build "$root/ReVision/ReVision.csproj" -c Release -p:PublishAot=false \
    --artifacts-path "$root/.local/artifacts/linux" --nologo "$@"
printf 'Built. Run: bash "%s/run-linux.sh"\n' "$root"
