#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
[[ "$(uname -s)" == Linux ]] || { printf 'This launcher requires Linux. Use Start-CameraTest.ps1 on Windows.\n' >&2; exit 1; }
command -v dotnet >/dev/null || { printf 'ASP.NET Core 10 runtime is required. Run linux/setup-arch.sh first.\n' >&2; exit 1; }
dll="$root/.local/artifacts/linux/bin/ReVision/release/ReVision.dll"
[[ -f "$dll" ]] || { printf 'Build first: bash "%s/build-linux.sh"\n' "$root" >&2; exit 1; }
# exec forwards Ctrl+C and systemd SIGTERM directly. There is no implicit deadline.
exec dotnet "$dll" "$@"
