@echo off
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-DotNet-Runtimes.ps1"
exit /b %ERRORLEVEL%
