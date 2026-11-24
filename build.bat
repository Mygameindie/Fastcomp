@echo off
rem Builds dist\PCTuneUp.exe using the C# compiler that ships with Windows (.NET Framework 4.x).
setlocal
cd /d "%~dp0"
set CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "src\app.ico" powershell -NoProfile -ExecutionPolicy Bypass -File "src\make-icon.ps1"
if not exist dist mkdir dist
"%CSC%" /nologo /target:winexe /optimize+ /platform:anycpu ^
  /out:dist\PCTuneUp.exe /win32icon:src\app.ico /win32manifest:src\app.manifest ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  src\PCTuneUp.cs
if errorlevel 1 (echo Build FAILED & exit /b 1)
echo Built dist\PCTuneUp.exe
