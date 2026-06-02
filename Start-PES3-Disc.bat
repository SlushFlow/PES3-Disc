@echo off
cd /d "%~dp0"
if exist "%~dp0dist\PES3-Disc.exe" (
    start "" "%~dp0dist\PES3-Disc.exe"
    exit /b 0
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%~dp0DiscRun.ps1"
