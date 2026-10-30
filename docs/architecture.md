# Architecture

`ReVision.Vive` opens the Vive HID interface and sends the upstream keep-alive response every 500 ms when a current authorization challenge is available. It drains queued status reports before choosing the challenge. `ReVision.EyeDevice` owns the EyeChip interface, negotiates the inherited protocol, opens the image realm, and decodes the Wearable Limited Image stream.

USB reads use a dedicated worker with a 500 ms timeout. Writes have a two-second timeout; command responses have a ten-second deadline and are removed from the pending map on every completion path. A transport failure cancels that device's workers. Managers remove disconnected devices and retry failed devices after five seconds. Handles are closed after their workers have ended.

The packet framer handles partial headers, partial bodies, and coalesced packets. Packets are bounded to 2 MiB, strings to 64 KiB, images to 1024 × 1024 grayscale pixels, region lists to 16 entries, and LED lists to 64. Unknown image columns are rejected rather than silently shifting the parser. Authentication material and protocol identifiers are inherited from ReVision v2; no vendor runtime or SDK binaries are bundled.

`ReVision.Dispatch` delivers producer-owned, immutable-by-contract frames and tracks monotonic frame ages. Subscribers must not modify pixel arrays. `ReVision.Streamer` has one lazy JPEG encoder per eye, shared among viewers. The input queue and each client queue hold only the latest frame. Idle streams do not encode. Eight viewers per eye and 32 HTTP connections limit resource use. The server cancels disconnected or stalled viewers without stopping capture.

ASP.NET Core/Kestrel provides the HTTP lifecycle. StbImageWriteSharp writes JPEGs directly from grayscale pixels without a native image dependency or a general-purpose image decoder. Encoding quality and maximum rate are configurable. Continuous capture has no implicit timer.

The projects retain their upstream names to keep protocol code recognizable and history useful. Automated tests exercise framing, malformed payloads, known wire bytes, HID CRC, frame dispatch, shared encoding, client limits, and HTTP shutdown using synthetic sources. Hardware checks remain a separate validation step.
