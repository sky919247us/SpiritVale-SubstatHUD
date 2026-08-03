@echo off
setlocal
if not exist "%~dp0install.ps1" goto :noscript
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
exit /b 0
:noscript
echo install.ps1 not found. Please extract all files into the same folder.
pause
exit /b 1
