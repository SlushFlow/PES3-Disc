$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'Ps3DiscRun.ps1')
$diy = Join-Path $PSScriptRoot 'diy-demo-disc'
$ret = Join-Path $PSScriptRoot 'retail-encrypted-disc'
$log = Join-Path $env:TEMP 'pes3-quick-scan.log'
Set-Pes3DiscRuntimeOptions -LogPath $log -PromptedVolumesPath (Join-Path $env:TEMP 'pes3-quick-prompted.json') -LockLogPath -LockPromptedVolumesPath
Write-Log 'pre-scan probe'
Write-Host "LogPath=$(Get-Pes3LogPath)"
Write-Host "Pre-scan log exists: $(Test-Path (Get-Pes3LogPath))"
$d = Get-TestVolumeDrives -Roots @($diy, $ret)
Write-Host "Test volumes: $($d.Count)"
Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diy, $ret) -NonInteractive
Write-Host "Log exists: $(Test-Path $log)"
if (Test-Path $log) { Get-Content $log }
