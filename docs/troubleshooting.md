# Troubleshooting

## Windows access denied

Normal launch never requests UAC or changes services. SRanipal, Tobii software, and other tracker applications may hold exclusive camera access. Close competing applications and check the `SRanipalService` and `Tobii VRU02 Runtime` services. Depending on the installed driver, Windows may also restrict access to the USB device itself.

Managing services requires permission from Windows. If you choose to stop competing services temporarily, open an administrator PowerShell 7 terminal yourself and run:

```powershell
.\Test-ExclusiveCamera.ps1
```

This explicit command records which services were running, stops only those services, runs the camera, and restores their running states in `finally`. It does not change service startup types. To end that session, create `logs/camera-test.stop` from another terminal. If the supervisor is interrupted or the machine loses power, run `.\Restore-Services.ps1` from an administrator terminal after the camera and supervisor have exited. Original service states are persisted before any service is changed. The recovery command refuses to run while the supervisor owns its lock.

Ordinary capture remains available through `Start-CameraTest.ps1` or `Start.ps1` without any automatic elevation. A script cannot grant itself device or service permissions without a system-level change.

## Headset detected, EyeChip missing

Check the link box, USB cable, headset power, and proximity sensor. On Windows, starting SteamVR can wake the EyeChip; after it appears, capture can continue without SteamVR. The keep-alive must use the newest HID authorization challenge; queued old reports can otherwise let the EyeChip disappear after several seconds.

## Server works but no images arrive

Read `/devices`: initialization errors, frame ages, and `IsStreaming` distinguish a connected HTTP server from live capture. Both eyes need frames less than two seconds old for streaming health. The app retries transport failures every five seconds and scans for device removal/reconnection once per second. Slow clients receive the latest available encoded frame; they do not build an unbounded queue.

Run `--diagnose` through the console launcher or `bash linux/doctor.sh`. A port conflict exits with an error; use `--port 4443` if needed. An unknown or disconnected device URL returns 404. Eight simultaneous viewers per eye are allowed; additional viewers receive 429. A camera wait or network write exceeding ten seconds closes that HTTP connection; reconnect the client when capture resumes.

## Logs and shutdown

Normal Windows launch writes `logs/camera.stdout.log` and `logs/camera.stderr.log`. Console mode logs to the terminal. Linux user service logs go to the journal. Logs can include serial numbers; redact them before posting. The app does not save eye images.

Sessions run until stopped unless an explicit duration is provided. Ctrl+C, SIGTERM on Linux, or the configured stop file initiates cleanup. A temporary camera error does not close the server. After a restart, stream clients may need to reconnect.
