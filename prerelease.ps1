# Refreshes the pre-release copy in OneDrive (<OneDrive>\AI\ClaudeCode\Flato3D_github_release):
# builds the package (package.ps1), then mirrors dist\<name> into <name> there (one folder name for every
# version) and copies the zip, which carries the version (<name>-<version>.zip).
# <name> is Flato3D-SR, or Flato3D-IntelARC with -Edition IntelARC (the Intel Arc / 3D One package).
# reports\ (problem reports from the installer) is never touched. While an installer runs from the
# pre-release folder its exe is locked: -Wait waits for it to close, otherwise nothing is changed.
# The version is the installer's AppInfo.Version; zips of other versions and old versioned folders are removed.
param([string]$Version = '', [switch]$Wait, [ValidateSet('SR', 'IntelARC')] [string]$Edition = 'SR')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $Version) {
    $Version = [regex]::Match((Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\src\Library.cs')), 'Version = "([^"]+)"').Groups[1].Value
}
$od = if ($env:OneDriveConsumer) { $env:OneDriveConsumer } else { $env:OneDrive }
if (-not $od) { throw 'OneDrive folder not found' }
$dst = Join-Path $od 'AI\ClaudeCode\Flato3D_github_release'
$name = if ($Edition -eq 'IntelARC') { 'Flato3D-IntelARC' } else { 'Flato3D-SR' }
$zip = "$name-$Version.zip"

function Running {
    Get-Process Flato3DInstaller -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path.StartsWith((Join-Path $dst $name), [StringComparison]::OrdinalIgnoreCase) }
}
& (Join-Path $root 'package.ps1') -Version $Version -Edition $Edition
if (Running) {
    if (-not $Wait) { "the installer is running from $dst\$name - close it, or use -Wait"; exit 1 }
    "waiting for the installer in $dst\$name to close..."
    while (Running) { Start-Sleep -Seconds 5 }
}
New-Item -ItemType Directory -Force $dst | Out-Null
Get-ChildItem $dst -Filter "$name-*" | Where-Object { $_.Name -ne $zip } | Remove-Item -Recurse -Force
robocopy (Join-Path $root "dist\$name") (Join-Path $dst $name) /MIR /R:2 /W:2 /NFL /NDL /NJH /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
Copy-Item (Join-Path $root "dist\$zip") (Join-Path $dst $zip) -Force
"pre-release: $dst\$name (+ $zip), $((Get-ChildItem (Join-Path $dst $name) -Recurse -File).Count) files"
exit 0   # robocopy's 1..7 mean copied / extra files, not failure
