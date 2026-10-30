# Third-party notices

Tobii Eye Feed is derived from [Blue-Doggo/ReVision-v2](https://github.com/Blue-Doggo/ReVision-v2), commit `51be11bba19766d7847d502b9ff3f9572bafbe14`, under GPL-3.0. The original Git history, root license, USB protocol, HID challenge/response, and realm material are retained. Fork modifications for release 0.2.0 are recorded in CHANGELOG.md and Git history. Original authors retain their copyrights.

| Component | Version / source | License / notice |
| --- | --- | --- |
| LibUsbDotNet | submodule `a01e2d8e78d8948217264c3e9d0cbda21be035ef` | Project declares LGPL-3.0-or-later; retain `LibUsbDotNet/LICENSE` and per-file notices (some older source declares GPL-2.0-or-later) |
| libusb | 1.0.30 Windows; distribution version on Linux | LGPL-2.1-or-later, `licenses/libusb.txt`; [matching source](https://github.com/libusb/libusb/tree/v1.0.30) |
| HIDAPI | 0.15.0 Windows; distribution version on Linux | BSD option selected, `licenses/HIDAPI-BSD.txt`; [matching source](https://github.com/libusb/hidapi/tree/hidapi-0.15.0) |
| HidApi.Net | 1.2.0 | MIT, `licenses/HidApi.Net.txt` |
| WCharT.Net | 0.1.3, transitive | MIT, `licenses/WCharT.Net.txt` |
| StbImageWriteSharp | 1.16.7 | Upstream README declares public domain; copied in `licenses/StbImageWriteSharp.txt` |
| stb_image_write | ported by StbImageWriteSharp | Public domain / MIT, `licenses/stb.txt` |
| System.IO.Hashing | 10.0.14 | MIT, `licenses/dotnet.txt` |
| .NET / ASP.NET Core | 10, installed separately | [Runtime license and notices](https://github.com/dotnet/runtime); not bundled by the framework-dependent release workflow |

Test and build tools include xUnit, Microsoft.NET.Test.Sdk, Nerdbank.GitVersioning, and SourceLink. They are not shipped as application runtime dependencies. Exact resolved packages and integrity hashes are recorded in `packages.lock.json` files. The application does not bundle Tobii, HTC, SRanipal, SteamVR, or inference model binaries.

Distributors should provide the corresponding source archive, including the LibUsbDotNet submodule, alongside binaries and retain all notices. Official libusb/HIDAPI DLLs are unmodified; source links above identify their matching versions. Dynamic libraries remain separate so users can replace them.
