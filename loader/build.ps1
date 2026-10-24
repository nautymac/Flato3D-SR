# Build bin\x64\version.dll and bin\x86\version.dll (Release) with the CMake bundled in Visual Studio, and the
# loader shims bin\<arch>\shim\<name>.dll (shim\, for games that import no version.dll).
$ErrorActionPreference = 'Stop'
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
$cmake = Join-Path $vs 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
foreach ($src in @($PSScriptRoot, (Join-Path $PSScriptRoot 'shim'))) {
    foreach ($t in @(@{ p = 'x64'; d = 'build' }, @{ p = 'Win32'; d = 'build-x86' })) {
        $dir = Join-Path $src $t.d
        & $cmake -S $src -B $dir -A $t.p | Out-Null
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        & $cmake --build $dir --config Release
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
Get-ChildItem (Join-Path $PSScriptRoot 'bin') -Recurse -Filter *.dll | ForEach-Object { Write-Host "built: $($_.FullName) ($($_.Length) bytes)" }
