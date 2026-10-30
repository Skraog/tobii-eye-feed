# HTTP interface

All routes are GET requests on `http://127.0.0.1:4442` by default. `--port` changes the port. Requests must use `127.0.0.1` or `localhost` as the host. Responses disable caching. No authentication or cross-origin access is provided; keep this service on loopback.

| Route | Response |
| --- | --- |
| `/` | Dashboard with camera previews and stream links |
| `/health` | `{"status":"ok","cameraVerified":false}`; cameraVerified becomes true when any tracker has recent frames from both eyes |
| `/devices` | JSON array of device snapshots, including devices still initializing or waiting to retry |
| `/{serial}/image/left` | MJPEG for the left eye |
| `/{serial}/image/right` | MJPEG for the right eye |

Snapshot fields use PascalCase: `DeviceId`, `SerialNumber`, `DeviceModel`, `Stage`, `Error`, `LeftFrames`, `RightFrames`, `LeftFrameAgeSeconds`, `RightFrameAgeSeconds`, `IsStreaming`, `LeftUrl`, `RightUrl`. Serial and URLs can be empty/null before identification. Counts restart when a device is reopened. A frame age is null until the first image arrives, then increases monotonically until the next frame. Stage text is descriptive; clients should use the explicit health fields instead of matching stage strings.

An MJPEG response has `Content-Type: multipart/x-mixed-replace; boundary=frame-boundary`. Each part contains `Content-Type: image/jpeg`, a `Content-Length`, the JPEG bytes, and a trailing CRLF. Device URLs must percent-encode the serial. Missing devices/invalid eyes return 404, disposed streams can return 503, and viewer limits return 429.

JPEG quality defaults to 85; `--jpeg-quality 1..100` changes it. `--max-fps 1..240` caps encoding, default 120. Actual rate is bounded by the hardware. Frame queues retain one image; slow consumers skip old frames. There is no timestamp metadata in the JPEG parts and no synchronization guarantee between separate HTTP eye streams.
