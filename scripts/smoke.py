"""Exercise the CLI and process lifecycle without USB hardware or third-party packages."""
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import sys
import tempfile
import time
import urllib.request

dll = str(Path(sys.argv[1]).resolve())

def port():
    with socket.socket() as listener:
        listener.bind(("127.0.0.1", 0))
        return listener.getsockname()[1]

def wait_ready(process, number):
    for _ in range(300):
        if process.poll() is not None:
            raise AssertionError(f"Server exited early: {process.returncode}")
        try:
            with urllib.request.urlopen(f"http://127.0.0.1:{number}/health", timeout=1) as response:
                assert json.load(response) == {"status": "ok", "cameraVerified": False}
            return
        except OSError:
            time.sleep(0.05)
    raise AssertionError("Server did not become ready")

assert subprocess.run(["dotnet", dll, "--invalid"], capture_output=True).returncode == 2
with tempfile.TemporaryDirectory() as temporary:
    for mode in (["stop", "duration", "signal"] if os.name != "nt" else ["stop", "duration"]):
        number = port()
        stop_file = Path(temporary) / f"{mode}.stop"
        args = ["dotnet", dll, "--no-hardware", "--port", str(number), "--stop-file", str(stop_file)]
        if mode == "duration":
            args += ["--duration", "10"]
        with tempfile.TemporaryFile(mode="w+") as log:
            process = subprocess.Popen(args, stdout=log, stderr=log)
            try:
                wait_ready(process, number)
                if mode == "duration":
                    process.wait(timeout=20)
                else:
                    time.sleep(3)
                    assert process.poll() is None, "Untimed run exited by itself"
                    if mode == "stop":
                        stop_file.touch()
                    else:
                        process.send_signal(signal.SIGTERM)
                    process.wait(timeout=20)
                assert process.returncode == 0, f"Unexpected exit code: {process.returncode}"
            except BaseException:
                log.seek(0)
                print(log.read())
                raise
            finally:
                if process.poll() is None:
                    process.kill()
                    process.wait()
print("CLI, health, duration, and shutdown smoke checks passed.")
