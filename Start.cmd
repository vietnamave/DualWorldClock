@echo off
setlocal
cd /d "%~dp0"
if exist "DualWorldClock.exe" goto run
call "%~dp0Build.cmd"
if errorlevel 1 exit /b 1
:run
start "" "%~dp0DualWorldClock.exe"
