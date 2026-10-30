#Requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') {
    throw 'The bundled native dependencies support Windows x64. Linux uses system libraries.'
}
$revisionRoot = Split-Path -Parent $PSScriptRoot
$nativeRoot = Join-Path $revisionRoot '.local/native'
$nativeBin = Join-Path $nativeRoot 'bin'
New-Item -ItemType Directory -Path $nativeRoot,$nativeBin -Force | Out-Null
function Get-VerifiedArchive([string]$Url, [string]$Path, [string]$Sha256) {
    if (!(Test-Path -LiteralPath $Path)) { Invoke-WebRequest -Uri $Url -OutFile $Path -UseBasicParsing }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Sha256) {
        throw "Checksum mismatch: $Path. Remove that archive and rerun Build.ps1."
    }
}

if (!(Test-Path -LiteralPath (Join-Path $nativeBin 'libusb-1.0.dll'))) {
    $archive = Join-Path $nativeRoot 'libusb-1.0.30.7z'
    Get-VerifiedArchive 'https://github.com/libusb/libusb/releases/download/v1.0.30/libusb-1.0.30.7z' $archive '7fb1dfec805b97983763d7d0ae244320da12add1003d4249c96cc4d586398c79'
    $sevenZipCommand = Get-Command 7z -ErrorAction SilentlyContinue
    $sevenZip = if ($sevenZipCommand) { $sevenZipCommand.Source } else { 'C:\Program Files\7-Zip\7z.exe' }
    if (!(Test-Path -LiteralPath $sevenZip)) { throw '7-Zip is needed to extract the official libusb archive.' }
    $extract = Join-Path $nativeRoot 'libusb'
    & $sevenZip x $archive "-o$extract" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'libusb extraction failed.' }
    Copy-Item -LiteralPath (Join-Path $extract 'VS2025\MS64-hotplug\dll\libusb-1.0.dll') -Destination $nativeBin
}
if (!(Test-Path -LiteralPath (Join-Path $nativeBin 'hidapi.dll'))) {
    $archive = Join-Path $nativeRoot 'hidapi-win.zip'
    Get-VerifiedArchive 'https://github.com/libusb/hidapi/releases/download/hidapi-0.15.0/hidapi-win.zip' $archive 'd18c43ec9506a2f6d7faa9c7e0a342c4b64fbae521b71b5d4ac0777fd24dda93'
    $extract = Join-Path $nativeRoot 'hidapi'
    Expand-Archive -LiteralPath $archive -DestinationPath $extract -Force
    Copy-Item -LiteralPath (Join-Path $extract 'x64\hidapi.dll') -Destination $nativeBin
}

# Keep native library license texts beside the DLLs.
$licenseRoot = Join-Path $nativeBin 'licenses'
New-Item -ItemType Directory -Path $licenseRoot -Force | Out-Null
foreach ($name in @('libusb.txt','HIDAPI.txt','HIDAPI-BSD.txt')) {
    Copy-Item -LiteralPath (Join-Path $revisionRoot "licenses/$name") -Destination $licenseRoot
}
Write-Host "Native libraries: $nativeBin"
