# Builds the release package dist\<name>-<version>.zip (folder dist\<name>, the same name for every version; only the
# zip carries the version) from the current builds:
#   installer\bin\Flato3DInstaller.exe (SR) or installer\bin\intelarc\Flato3DInstaller.exe (-Edition IntelARC),
#   ogl3d\bin\{x86,x64}\opengl32.dll, loader\bin\{x86,x64}\version.dll and shim\winmm.dll,
#   third_party\wined3d-11.19, data\game_profiles.json, data\game_candidates.json.
# Run installer\src\build.cmd (and build.cmd intelarc), ogl3d\build.ps1 and loader\build.ps1 first.
# The version (and its date, written into README.txt) is the installer's AppInfo (installer\src\Library.cs).
# Editions: SR (Flato3D-SR: SR displays, SBS, top and bottom) and IntelARC (Flato3D-IntelARC: Intel Arc / 3D One,
# side by side woven by AstraDepth3D; the ini template's output is sbs).
param([string]$Version = '', [ValidateSet('SR', 'IntelARC')] [string]$Edition = 'SR')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$info = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\src\Library.cs')
$appVersion = [regex]::Match($info, 'Version = "([^"]+)"').Groups[1].Value
$appDate = [regex]::Match($info, 'Date = "([^"]+)"').Groups[1].Value
if (-not $Version) { $Version = $appVersion }
if ($Version -ne $appVersion) { throw "version $Version is not the installer's AppInfo.Version $appVersion" }
$intel = $Edition -eq 'IntelARC'
$name = if ($intel) { 'Flato3D-IntelARC' } else { 'Flato3D-SR' }   # the folder, inside the zip too: unpacking a new version replaces the old one in place
$stage = Join-Path $root "dist\$name"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
function Put([string]$src, [string]$dst) {
    $from = Join-Path $root $src
    if (-not (Test-Path $from)) { throw "missing $src" }
    $to = Join-Path $stage $dst
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    Copy-Item $from $to
}
Put $(if ($intel) { 'installer\bin\intelarc\Flato3DInstaller.exe' } else { 'installer\bin\Flato3DInstaller.exe' }) 'Flato3DInstaller.exe'
Put $(if ($intel) { 'installer\README-IntelARC.txt' } else { 'installer\README.txt' }) 'README.txt'
$readme = Join-Path $stage 'README.txt'   # first line: the version
$text = [IO.File]::ReadAllText($readme, (New-Object Text.UTF8Encoding $false))
$text = ([regex]"^$name - ").Replace($text, "$name $Version ($appDate) - ", 1)
[IO.File]::WriteAllText($readme, $text, (New-Object Text.UTF8Encoding $false))
Put 'installer\THIRD_PARTY.txt' 'THIRD_PARTY.txt'
Put 'LICENSE' 'LICENSE'
Put 'data\game_profiles.json' 'data\game_profiles.json'
Put 'data\game_candidates.json' 'data\game_candidates.json'
Put 'ogl3d\OGL3D.ini' 'ogl3d\OGL3D.ini'
if ($intel) {
    # no SR runtime on the 3D One: the template's output is side by side (the installer writes it on install as well)
    $ini = Join-Path $stage 'ogl3d\OGL3D.ini'
    $t = [IO.File]::ReadAllText($ini, (New-Object Text.UTF8Encoding $false))
    $t = [regex]::Replace($t, '(?m)^output = auto', 'output = sbs')
    [IO.File]::WriteAllText($ini, $t, (New-Object Text.UTF8Encoding $false))
}
Put 'ogl3d\LICENSE' 'ogl3d\LICENSE'
foreach ($a in 'x86', 'x64') {
    Put "ogl3d\bin\$a\opengl32.dll" "ogl3d\$a\opengl32.dll"
    Put "loader\bin\$a\version.dll" "loader\$a\version.dll"
    Put "loader\bin\$a\shim\winmm.dll" "loader\$a\shim\winmm.dll"
    foreach ($f in 'wined3d.dll', 'd3d9.dll', 'd3d8.dll', 'ddraw.dll') { Put "third_party\wined3d-11.19\$a\$f" "wined3d\$a\$f" }
    foreach ($f in 'wined3d.dll', 'w3d11.dll', 'wxgi.dll', 'w3d10core.dll', 'd3d10.dll', 'd3d10_1.dll') { Put "third_party\wined3d-11.19\$a-renamed\$f" "wined3d\$a-renamed\$f" }
}
Put 'third_party\wined3d-11.19\SOURCE.txt' 'wined3d\SOURCE.txt'
Put 'third_party\wined3d-11.19\x86\WINED3D README.txt' 'wined3d\WINED3D README.txt'
Put 'third_party\licenses\LGPL-2.1.txt' 'wined3d\LGPL-2.1.txt'

$zip = Join-Path $root "dist\$name-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $rel = $name + '/' + $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $_.FullName, $rel, 'Optimal') | Out-Null   # keeps the file time
    }
} finally { $z.Dispose() }
"package: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"
