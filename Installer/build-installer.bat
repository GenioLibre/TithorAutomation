@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-installer.ps1"
set "buildResult=%ERRORLEVEL%"
pause
exit /b %buildResult%
