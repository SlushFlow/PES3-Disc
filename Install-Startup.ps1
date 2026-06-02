# Adds PES3-Disc to the current user's Windows Startup folder.
$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$batPath = Join-Path $scriptDir 'Start-PES3-Disc.bat'
$startup = [Environment]::GetFolderPath('Startup')
$linkPath = Join-Path $startup 'PES3-Disc.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($linkPath)
$shortcut.TargetPath = $batPath
$shortcut.WorkingDirectory = $scriptDir
$shortcut.WindowStyle = 7
$shortcut.Description = 'Prompt to run PS3 discs in RPCS3 when inserted'
$shortcut.Save()

Write-Host "Startup shortcut created:"
Write-Host "  $linkPath"
Write-Host ""
Write-Host "PES3-Disc will start when you log in to Windows."
Write-Host "Remove that shortcut from Startup to disable."
