# Interactive setup: choose RPCS3 path and write config.json
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
. (Join-Path $PSScriptRoot 'Ps3DiscRun.Common.ps1')

$config = Get-Config
$current = if ($config) { $config.Rpcs3Path } else { '' }

$dialog = New-Object System.Windows.Forms.OpenFileDialog
$dialog.Filter = 'RPCS3 (rpcs3.exe)|rpcs3.exe|All files (*.*)|*.*'
$dialog.Title = 'Select rpcs3.exe'
$dialog.FileName = if ($current) { Split-Path $current -Leaf } else { 'rpcs3.exe' }
if ($current) { $dialog.InitialDirectory = Split-Path $current -Parent }

if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
    Write-Host 'Cancelled.'
    exit 1
}

$delay = 3
$noGui = $false
if ($config) {
    if ($config.ScanDelaySeconds) { $delay = [int]$config.ScanDelaySeconds }
    if ($null -ne $config.UseNoGui) { $noGui = [bool]$config.UseNoGui }
}

Save-Config -Rpcs3Path $dialog.FileName -ScanDelaySeconds $delay -UseNoGui $noGui
Write-Host "Saved config.json"
Write-Host "  RPCS3: $($dialog.FileName)"
Write-Host ""
Write-Host "Run Start-PES3-Disc.bat or Install-Startup.ps1 to enable disc prompts."
