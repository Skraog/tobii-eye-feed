# Security

The camera HTTP service is intended for a single local user. It binds to IPv4 loopback, validates the request host, limits clients and queues, and does not enable CORS. Any local process with access to the port may read the camera. Do not proxy it to the internet or a shared network without a separate authenticated gateway.

Use GitHub's private vulnerability reporting for security issues in [Skraog/tobii-eye-feed](https://github.com/Skraog/tobii-eye-feed/security/advisories/new). If private reporting is unavailable, open an issue asking for a private contact without including exploit details or private images. Include the affected commit, platform, and a minimal reproducer where possible. Hardware serials and eye images should be redacted.

Only the current default branch receives fixes. Dependencies are pinned and checked in CI; a successful audit means no advisories were reported by the configured feed at that time, not a guarantee that every dependency is defect-free.
