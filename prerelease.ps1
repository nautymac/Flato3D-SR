# Refreshes the pre-release copy in OneDrive (<OneDrive>\AI\ClaudeCode\Flato3D_github_release):
# builds the package (package.ps1), then mirrors dist\Flato3D-SR into Flato3D-SR there (one folder name for every
# version) and copies the zip, which carries the version (Flato3D-SR-<version>.zip).
# reports\ (problem reports from the installer) is never touched. While an installer runs from the
# pre-release folder its exe is locked: -Wait waits for it to close, otherwise nothing is changed.
# The version is the installer's AppInfo.Version; zips of other versions and old versioned folders are removed.
param([string]$Version = '', [switch]$Wait)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $Version) {
    $Version = [regex]::Match((Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\src\Library.cs')), 'Version = "([^"]+)"').Groups[1].Value
}
$od = if ($env:OneDriveConsumer) { $env:OneDriveConsumer } else { $env:OneDrive }
if (-not $od) { throw 'OneDrive folder not found' }
$dst = Join-Path $od 'AI\ClaudeCode\Flato3D_github_release'
$name = "Flato3D-SR"
$zip = "$name-$Version.zip"

function Running {
    Get-Process Flato3DInstaller -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith($dst, [StringComparison]::OrdinalIgnoreCase) }
}
& (Join-Path $root 'package.ps1') -Version $Version
if (Running) {
    if (-not $Wait) { "the installer is running from $dst - close it, or use -Wait"; exit 1 }
    "waiting for the installer in $dst to close..."
    while (Running) { Start-Sleep -Seconds 5 }
}
New-Item -ItemType Directory -Force $dst | Out-Null
Get-ChildItem $dst -Filter 'Flato3D-SR-*' | Where-Object { $_.Name -ne $zip } | Remove-Item -Recurse -Force
robocopy (Join-Path $root "dist\$name") (Join-Path $dst $name) /MIR /R:2 /W:2 /NFL /NDL /NJH /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
Copy-Item (Join-Path $root "dist\$zip") (Join-Path $dst $zip) -Force
"pre-release: $dst\$name (+ $zip), $((Get-ChildItem (Join-Path $dst $name) -Recurse -File).Count) files"
exit 0   # robocopy's 1..7 mean copied / extra files, not failure
