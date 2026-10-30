# Linux support

The application uses .NET 10, libusb for EyeChip USB, and HIDAPI's hidraw backend for the Vive keep-alive. It does not depend on Wine, SRanipal, or a Linux video driver. Capture on physical Linux hardware remains unverified; software checks under WSL do not establish headset support.

## Dependencies and permissions

On Arch/EndeavourOS, `bash linux/setup-arch.sh` installs `dotnet-sdk-10.0`, `aspnet-runtime-10.0`, `libusb`, `hidapi`, and `usbutils`, then the scoped udev rule. The installer uses sudo only for package/rule installation. Running capture never requests elevation.

For manual setup, install those packages and copy `linux/70-revision-vive-pro-eye.rules` to `/etc/udev/rules.d/`. Reload udev rules and reconnect the headset. The rule grants access to the active local desktop user for Vive USB `0bb4:0309`, its hidraw interfaces, and EyeChip `2104:020f`. It does not grant all users access to every USB device. Remote/headless logins need an administrator-managed device access policy; do not run the application as root.

Other distributions need the .NET 10 SDK for builds, ASP.NET Core 10 runtime for execution, libusb (`libusb-1.0.so.0`), and HIDAPI's hidraw backend (`libhidapi-hidraw.so.0`). Debian/Ubuntu package names generally include `libusb-1.0-0` and `libhidapi-hidraw0`. Install a supported .NET SDK from your distribution or Microsoft's official packages. No kernel module should be detached from the Vive HID interface.

```bash
bash build-linux.sh
bash linux/doctor.sh
bash run-linux.sh
```

The doctor checks library loading and USB/HID access. It does not initialize the camera protocol or prove image capture. Run the app and check fresh frames on the dashboard for that.

Windows and Linux intermediates live in separate `.local/artifacts/` directories, so sharing a checkout through WSL does not overwrite Windows executables. Do not forward a Windows headset to WSL while Windows capture is using it.

## Headset wake-up

The HID keep-alive maintains an already visible EyeChip. Whether a cold headset needs additional Linux-specific wake-up commands is not yet established. If `lsusb` shows the Vive but no `2104:020f`, record the power/link-box state and SteamVR state when reporting it. Continuous capture, unplug/replug, suspend/resume, and cold boot need physical Linux testing.

## Optional user service

After building and validating manual capture:

```bash
bash linux/install-user-service.sh
systemctl --user start revision-camera
journalctl --user -u revision-camera -f
systemctl --user stop revision-camera
```

The installer writes the current checkout path to the user service; run it again after moving the checkout. Enable it with `systemctl --user enable revision-camera` if automatic startup with the graphical session is desired. SIGTERM stops the app and releases USB/HID handles. USB errors are shown in diagnostics and retried; an unexpected process exit is restarted after five seconds.
