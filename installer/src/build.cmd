@echo off
:: Builds ..\bin\Flato3DInstaller.exe (WinForms, .NET Framework 4.x, no console).
:: Uses the Roslyn csc bundled with Visual Studio (C# 7) when present, else the .NET Framework 4 csc.
setlocal
set OUT=%~dp0..\bin\Flato3DInstaller.exe
if not exist "%~dp0..\bin" mkdir "%~dp0..\bin"
set CSC=
for /f "usebackq delims=" %%i in (`"%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath 2^>nul`) do set VS=%%i
if defined VS if exist "%VS%\MSBuild\Current\Bin\Roslyn\csc.exe" set CSC=%VS%\MSBuild\Current\Bin\Roslyn\csc.exe
if not defined CSC set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
echo csc: %CSC%
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /platform:anycpu /win32manifest:"%~dp0app.manifest" /win32icon:"%~dp0app.ico" /out:"%OUT%" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Management.dll "%~dp0Flato3DInstaller.cs" "%~dp0Library.cs" "%~dp0Setup.cs" "%~dp0Report.cs" "%~dp0SystemCheck.cs"
if errorlevel 1 (echo BUILD FAILED & exit /b 1)
echo built: %OUT%
