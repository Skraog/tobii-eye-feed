# Changelog

## 0.2.0 — 2026-10-30

- Fork ReVision v2 as Tobii Eye Feed, preserving the USB protocol and upstream license.
- Support continuous camera sessions, explicit timed runs, graceful shutdown, and device reconnection.
- Run normal Windows capture without automatic elevation or service changes; retain explicit service recovery tools.
- Add Arch/EndeavourOS dependencies, scoped udev permissions, diagnostics, and an optional user service.
- Bound packet parsing, command waits, frame queues, and HTTP clients.
- Share JPEG encoding across clients and expose frame freshness.
- Replace EmbedIO and ImageSharp with ASP.NET Core and a managed stb JPEG writer.
- Add locked dependencies, Windows/Linux tests, CI, and source/binary artifact workflows.

Physical Linux camera capture and cold wake-up remain pending validation.
