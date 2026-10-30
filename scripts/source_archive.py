"""Create corresponding source from a clean checkout, including initialized submodules."""
from pathlib import Path
import subprocess
import sys
import tarfile

root = Path(__file__).resolve().parent.parent
output = Path(sys.argv[1]).resolve()
if subprocess.check_output(["git", "status", "--porcelain"], cwd=root):
    raise SystemExit("Commit or stash changes before making a release source archive.")
paths = subprocess.check_output(["git", "ls-files", "--recurse-submodules", "-z"], cwd=root).decode().split("\0")
output.parent.mkdir(parents=True, exist_ok=True)
with tarfile.open(output, "w:gz") as archive:
    for name in sorted(filter(None, paths)):
        path = root / name
        if not path.is_file():
            raise SystemExit(f"Missing tracked source: {name}; initialize submodules first.")
        if path.resolve() == output:
            raise SystemExit("Source archive cannot contain itself.")
        archive.add(path, arcname=f"tobii-eye-feed/{name}", recursive=False)
print(output)
