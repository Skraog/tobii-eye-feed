# Contributing

Clone with `--recurse-submodules` and use the SDK in `global.json`. Initialize an existing clone with `git submodule update --init --recursive`. Preserve upstream protocol attribution and dependency notices. Do not include camera captures, serials, caches, IDE files, or local logs in commits.

## Checks

```text
dotnet test ReVision.Tests/ReVision.Tests.csproj -c Release --artifacts-path .local/artifacts/check -p:RestoreLockedMode=true
dotnet format whitespace ReVision.sln --exclude LibUsbDotNet --verify-no-changes
python scripts/smoke.py .local/artifacts/check/bin/ReVision/release/ReVision.dll
python scripts/audit.py ReVision.Tests/ReVision.Tests.csproj
```

On Windows, `Build.ps1` also downloads the pinned native libraries needed for hardware diagnostics. Linux uses system packages. The HTTP tests use synthetic frames and do not require native libraries or hardware. Update lock files intentionally when changing packages, then run the vulnerability audit. Keep the LibUsbDotNet submodule pinned and avoid editing its vendored source as part of unrelated changes.

Include a regression test for transport or lifecycle fixes. For changes to initialization, HID timing, or image decoding, also verify both real eye streams, a multi-minute session, and a clean stop/restart. Record OS, firmware/model, and frame rate; avoid publishing headset serials or images. Linux hardware claims require Linux hardware evidence.

## Release artifacts

`scripts/Package.ps1 -Runtime win-x64` and `-Runtime linux-x64` produce framework-dependent archives in `artifacts/`. Run from PowerShell 7 on the matching OS; Windows packaging needs 7-Zip. Packages include license texts and require the ASP.NET Core 10 runtime. Linux packages use the host's libusb/HIDAPI, not bundled native libraries.

`python scripts/source_archive.py artifacts/tobii-eye-feed-source.tar.gz` creates corresponding source including the pinned submodule from a clean committed checkout. Publish that source alongside binary archives. CI checks Windows and Linux; the artifact workflow runs on version tags or manual dispatch and uploads packages, source, and SHA-256 checksums without creating a public release. Review hardware validation and the changelog before distributing a release.
