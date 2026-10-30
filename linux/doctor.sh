#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
printf 'Kernel: %s\n' "$(uname -sr)"
printf 'User: %s\n' "$(id)"
if grep -qi microsoft /proc/sys/kernel/osrelease; then
    printf 'WSL detected: native library/HTTP checks are useful, but USB capture requires USB forwarding and does not establish native Arch support.\n'
fi
if command -v lsusb >/dev/null; then
    lsusb -d 0bb4:0309 || true
    lsusb -d 2104:020f || true
else
    printf 'lsusb unavailable; Arch package: usbutils.\n'
fi
if [[ -f /etc/udev/rules.d/70-revision-vive-pro-eye.rules ]]; then
    printf 'ReVision udev rule is installed. Reconnect after rule changes.\n'
else
    printf 'ReVision udev rule is missing. Run linux/setup-arch.sh on Arch/EndeavourOS.\n'
fi
if command -v getfacl >/dev/null; then
    for path in /dev/hidraw*; do
        [[ -e "$path" ]] || continue
        if udevadm info --attribute-walk --name="$path" | grep 'ATTRS{idProduct}=="0309"' >/dev/null; then
            getfacl -- "$path"
        fi
    done
fi
exec bash "$root/run-linux.sh" --diagnose
