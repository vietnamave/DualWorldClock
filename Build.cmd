@echo off
setlocal
cd /d "%~dp0"
set "CLOCK_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CLOCK_CSC%" set "CLOCK_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CLOCK_CSC%" (
  echo Microsoft .NET Framework 4.x compiler was not found.
  echo Please contact your PC administrator.
  pause
  exit /b 1
)
echo Building Dual World Clock 1.0.0...
"%CLOCK_CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:"DualWorldClock.exe" /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll "DualWorldClock.cs" > "build-log.txt" 2>&1
if errorlevel 1 (
  echo Build failed. Close the running clock and try again.
  type "build-log.txt"
  pause
  exit /b 1
)
echo Build complete.
exit /b 0
