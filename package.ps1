# Builds the Flato3D-SR release package dist\Flato3D-SR-<version>.zip from the current builds:
#   installer\bin\Flato3DInstaller.exe, ogl3d\bin\{x86,x64}\opengl32.dll, loader\bin\{x86,x64}\version.dll,
#   third_party\wined3d-11.19, data\game_profiles.json, data\game_candidates.json.
# Run installer\src\build.cmd, ogl3d\build.ps1 and loader\build.ps1 first.
param([string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$name = "Flato3D-SR-$Version"
$stage = Join-Path $root "dist\$name"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
function Put([string]$src, [string]$dst) {
    $from = Join-Path $root $src
    if (-not (Test-Path $from)) { throw "missing $src" }
    $to = Join-Path $stage $dst
    New-Item -ItemType Directory -Force (Split-Path $to) | Out-Null
    Copy-Item $from $to
}
Put 'installer\bin\Flato3DInstaller.exe' 'Flato3DInstaller.exe'
Put 'installer\README.txt' 'README.txt'
Put 'installer\THIRD_PARTY.txt' 'THIRD_PARTY.txt'
Put 'LICENSE' 'LICENSE'
Put 'data\game_profiles.json' 'data\game_profiles.json'
Put 'data\game_candidates.json' 'data\game_candidates.json'
Put 'ogl3d\OGL3D.ini' 'ogl3d\OGL3D.ini'
Put 'ogl3d\LICENSE' 'ogl3d\LICENSE'
foreach ($a in 'x86', 'x64') {
    Put "ogl3d\bin\$a\opengl32.dll" "ogl3d\$a\opengl32.dll"
    Put "loader\bin\$a\version.dll" "loader\$a\version.dll"
    foreach ($f in 'wined3d.dll', 'd3d9.dll', 'd3d8.dll', 'ddraw.dll') { Put "third_party\wined3d-11.19\$a\$f" "wined3d\$a\$f" }
    foreach ($f in 'wined3d.dll', 'w3d11.dll', 'wxgi.dll', 'w3d10core.dll', 'd3d10.dll', 'd3d10_1.dll') { Put "third_party\wined3d-11.19\$a-renamed\$f" "wined3d\$a-renamed\$f" }
}
Put 'third_party\wined3d-11.19\SOURCE.txt' 'wined3d\SOURCE.txt'
Put 'third_party\wined3d-11.19\x86\WINED3D README.txt' 'wined3d\WINED3D README.txt'
Put 'third_party\licenses\LGPL-2.1.txt' 'wined3d\LGPL-2.1.txt'

$zip = Join-Path $root "dist\$name.zip"
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
