"""Fail when NuGet reports a vulnerable package, including transitive dependencies."""
import json
import subprocess
import sys

project = sys.argv[1] if len(sys.argv) > 1 else "ReVision.Tests/ReVision.Tests.csproj"
result = subprocess.run(["dotnet", "list", project, "package", "--vulnerable", "--include-transitive", "--format", "json"],
                        capture_output=True, text=True, check=True)
report = json.loads(result.stdout)
problems = []
for item in report.get("projects", []):
    for framework in item.get("frameworks", []):
        for kind in ("topLevelPackages", "transitivePackages"):
            for package in framework.get(kind, []):
                if package.get("vulnerabilities"):
                    problems.append(package)
if report.get("problems") or problems:
    print(json.dumps(report, indent=2))
    sys.exit(1)
print("NuGet reported no vulnerable packages.")
