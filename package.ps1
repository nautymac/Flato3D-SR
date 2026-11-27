# Builds the release package dist\<name>-<version>.zip (folder dist\<name>, the same name for every version; only the
# zip carries the version) from the current builds:
#   installer\bin\Flato3DInstaller.exe (one installer: SR mode or Intel Arc mode, chosen on the PC it runs on),
#   ogl3d\bin\{x86,x64}\opengl32.dll, loader\bin\{x86,x64}\version.dll and shim\winmm.dll,
#   third_party\wined3d-11.19, data\game_profiles.json, data\game_candidates.json.
# Run installer\src\build.cmd, ogl3d\build.ps1 and loader\build.ps1 first.
# The version (and its date, written into README.txt) is the installer's AppInfo (installer\src\Library.cs).
# One package for SR displays (NVIDIA) and the 3D One (Intel Arc, side by side woven by AstraDepth3D): the installer
# writes the output into each game's OGL3D.ini, so the template keeps output = auto. README-IntelARC.txt is the guide
# to Intel Arc mode.
param([string]$Version = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$info = Get-Content -Raw -Encoding UTF8 (Join-Path $root 'installer\src\Library.cs')
$appVersion = [regex]::Match($info, 'Version = "([^"]+)"').Groups[1].Value
$appDate = [regex]::Match($info, 'Date = "([^"]+)"').Groups[1].Value
if (-not $Version) { $Version = $appVersion }
if ($Version -ne $appVersion) { throw "version $Version is not the installer's AppInfo.Version $appVersion" }
$name = 'Flato3D-SR'   # the folder, inside the zip too: unpacking a new version replaces the old one in place
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
Put 'installer\README-IntelARC.txt' 'README-IntelARC.txt'
foreach ($r in @(@{ f = 'README.txt'; n = 'Flato3D-SR' }, @{ f = 'README-IntelARC.txt'; n = 'Flato3D-IntelARC' })) {
    $readme = Join-Path $stage $r.f   # first line: the version
    $text = [IO.File]::ReadAllText($readme, (New-Object Text.UTF8Encoding $false))
    $text = ([regex]"^$($r.n) - ").Replace($text, "$($r.n) $Version ($appDate) - ", 1)
    [IO.File]::WriteAllText($readme, $text, (New-Object Text.UTF8Encoding $false))
}
Put 'installer\THIRD_PARTY.txt' 'THIRD_PARTY.txt'
Put 'LICENSE' 'LICENSE'
Put 'data\game_profiles.json' 'data\game_profiles.json'
Put 'data\game_candidates.json' 'data\game_candidates.json'
Put 'ogl3d\OGL3D.ini' 'ogl3d\OGL3D.ini'
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
# Linux (DroidDeck / Steam Deck desktop, Proton games): the same profiles and payload
foreach ($f in 'flato3d-linux.py', 'install-desktop.sh', 'README.md') { Put "linux\$f" "linux\$f" }

$zip = Join-Path $root "dist\$name-$Version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
# Windows Defender's cloud verdict on the loader (version.dll, a known false positive) sometimes stops the zip
# being written ("file contains a virus"): the zip is built in the temp folder and tried a few times
$tmp = Join-Path ([IO.Path]::GetTempPath()) "$name-$Version.zip"
for ($try = 1; $try -le 4; ++$try) {
    if (Test-Path $tmp) { Remove-Item $tmp -Force }
    try {
        $z = [System.IO.Compression.ZipFile]::Open($tmp, 'Create')
        try {
            Get-ChildItem $stage -Recurse -File | ForEach-Object {
                $rel = $name + '/' + $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($z, $_.FullName, $rel, 'Optimal') | Out-Null   # keeps the file time
            }
        } finally { $z.Dispose() }
        Move-Item $tmp $zip -Force
        break
    } catch {
        if ($try -eq 4) { throw }
        "zip attempt $try failed ($($_.Exception.Message.Trim())), retrying..."
        Start-Sleep -Seconds 3
    }
}
"package: $zip ($([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB)"
