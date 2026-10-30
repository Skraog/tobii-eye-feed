#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
[[ -r /etc/os-release ]] || { printf 'Cannot detect the distribution.\n' >&2; exit 1; }
. /etc/os-release
if [[ "${ID:-}" != arch && " ${ID_LIKE:-} " != *" arch "* ]]; then
    printf 'This dependency installer is for Arch/EndeavourOS. Install .NET 10 SDK/runtime, libusb, and libhidapi-hidraw.so.0 using your distribution.\n' >&2
    exit 1
fi
sudo pacman -S --needed dotnet-sdk-10.0 aspnet-runtime-10.0 libusb hidapi usbutils
sudo install -m 0644 "$root/linux/70-revision-vive-pro-eye.rules" /etc/udev/rules.d/70-revision-vive-pro-eye.rules
sudo udevadm control --reload-rules
printf 'Dependencies and scoped udev rules installed. Reconnect the headset from your desktop session.\n'
printf 'Build: bash "%s/build-linux.sh"\nDiagnose: bash "%s/linux/doctor.sh"\n' "$root" "$root"
