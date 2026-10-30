# Tobii Eye Feed

Vive Pro Eye camera passthrough for Windows and Linux. Exposes the two internal infrared eye cameras as local MJPEG streams for applications that perform their own inference, such as compatible Baballonia builds.

This is a fork of [Blue-Doggo/ReVision-v2](https://github.com/Blue-Doggo/ReVision-v2). The USB protocol and Vive keep-alive are based on that project. The fork adds bounded streaming, device reconnection, graceful shutdown, Linux setup, and automated tests.

## Status and requirements

- Hardware: **HTC Vive Pro Eye internal eye cameras**. Other Tobii devices are not validated. This does not support the separate Vive Facial Tracker.
- Windows x64 capture was verified with 200 × 200 grayscale images from each eye. Capture and restart after an idle interval were verified with SteamVR closed. Wake-up after a full headset power cycle still needs validation; some setups may need SteamVR initially.
- Linux x64 builds, automated tests, HTTP streaming, native dependency loading, and shutdown are tested. **Native Linux headset capture and wake-up still need validation**, including on Arch/EndeavourOS.
- .NET 10 SDK for building; ASP.NET Core 10 runtime for running framework-dependent builds. PowerShell 7.2+ and 7-Zip for Windows source builds. Linux uses Bash, libusb, and HIDAPI's hidraw backend.

This provides camera images, not gaze estimates, calibration, OSC, or an SRanipal-compatible API. MJPEG input support depends on the receiving application's version. It does not register a webcam device automatically.

## Windows

Clone the fork including its USB submodule:

```powershell
git clone --recurse-submodules https://github.com/Skraog/tobii-eye-feed.git
cd tobii-eye-feed
.\Build.ps1
.\Start-CameraTest.ps1
```

Open <http://127.0.0.1:4442/>. Start and stop run as your current user; there is no automatic UAC prompt or service change. Stop with `.\Stop-CameraTest.ps1`. The default session has no time limit. `-PreviewSeconds 60` requests an explicit timed run.

SRanipal and Tobii services can hold exclusive camera access. If Windows denies access, see [troubleshooting](docs/troubleshooting.md). Building and ordinary capture do not require administrator rights; managing Windows services can require them.

For a console session with application options:

```powershell
.\Start.ps1 --jpeg-quality 80 --max-fps 90
```

## Arch / EndeavourOS

```bash
git clone --recurse-submodules https://github.com/Skraog/tobii-eye-feed.git
cd tobii-eye-feed
bash linux/setup-arch.sh
bash build-linux.sh
bash run-linux.sh
```

The setup command installs packages and a scoped udev rule. Those system changes request sudo once; **running the app never invokes sudo**. Reconnect the headset after installing the rule and run from an active local desktop login. If permissions and dependencies are already configured, skip setup. Manual installation, other distributions, and optional user service setup are in [Linux support](docs/linux.md).

## Streams and diagnostics

The dashboard links to each eye. `GET /devices` returns the current serial, state, errors, frame counts, frame ages, and stream URLs. A typical URL is:

```text
http://127.0.0.1:4442/<serial-number>/image/left
http://127.0.0.1:4442/<serial-number>/image/right
```

Copy the actual URL from the dashboard into an application that accepts HTTP MJPEG. `/health` reports server availability and whether both eyes have recent camera frames. A healthy server alone does not establish camera capture. See [API details](docs/api.md).

The server binds to IPv4 loopback only. Images stay in memory and are sent to connected local clients. Keep the endpoint local: it has no authentication. Avoid sharing logs that contain headset serials or camera images.

## Building and contributing

The SDK policy accepts stable .NET 10 feature bands, including distribution SDKs; CI pins SDK 10.0.403. NuGet dependencies are locked per project. Windows native downloads use pinned versions and SHA-256 checksums. CI builds and tests on Windows and Linux; it cannot verify a physical headset.

See [contributing](CONTRIBUTING.md) for tests, formatting, and release packaging, and [architecture](docs/architecture.md) for transport and resource limits.

## License

[GPL-3.0](LICENSE), retaining ReVision v2's license and upstream attribution. Fork changes are described in [CHANGELOG.md](CHANGELOG.md). Dependency notices and license texts are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and `licenses/`. Provided without warranty. Not affiliated with HTC or Tobii.
