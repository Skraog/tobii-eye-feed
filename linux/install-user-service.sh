#!/usr/bin/env bash
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
mkdir -p -- "$HOME/.config/systemd/user"
# systemd paths require quoting; reject control characters and escape its specifiers.
[[ "$root" != *$'\n'* && "$root" != *$'\r'* ]] || exit 1
escaped="${root//%/%%}"
escaped="${escaped//\\/\\\\}"
escaped="${escaped//\"/\\\"}"
escaped="${escaped//&/\\&}"
sed "s|@ROOT@|${escaped//|/\\|}|g" "$root/linux/revision-camera.service.in" > "$HOME/.config/systemd/user/revision-camera.service"
systemctl --user daemon-reload
printf 'Installed. Start with: systemctl --user start revision-camera\nLogs: journalctl --user -u revision-camera -f\n'
