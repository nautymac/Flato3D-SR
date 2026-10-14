# Build bin\x64\version.dll and bin\x86\version.dll (Release) with the CMake bundled in Visual Studio.
$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$cmake = Join-Path $vs 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
foreach ($t in @(@{ p = 'x64'; d = 'build' }, @{ p = 'Win32'; d = 'build-x86' })) {
    $dir = Join-Path $PSScriptRoot $t.d
    & $cmake -S $PSScriptRoot -B $dir -A $t.p | Out-Null
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $cmake --build $dir --config Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
Get-ChildItem (Join-Path $PSScriptRoot 'bin') -Recurse -Filter version.dll | ForEach-Object { Write-Host "built: $($_.FullName) ($($_.Length) bytes)" }
