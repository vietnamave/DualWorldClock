@echo off
setlocal
cd /d "%~dp0"
call "%~dp0Build.cmd"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -Command "$ErrorActionPreference='Stop'; Compress-Archive -LiteralPath 'DualWorldClock.exe','DualWorldClock.exe.config','Start.cmd','Build.cmd','DualWorldClock.cs','README.md','LICENSE.txt','CHANGELOG.md' -DestinationPath 'DualWorldClock-1.0.0-Windows.zip' -Force"
if errorlevel 1 (
  echo Packaging failed.
  pause
  exit /b 1
)
echo Created: DualWorldClock-1.0.0-Windows.zip
pause
