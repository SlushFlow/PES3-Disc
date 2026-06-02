$ErrorActionPreference = 'Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'Ps3DiscRun.ps1')
$diy = Join-Path $PSScriptRoot 'diy-demo-disc'
$log = Join-Path $env:TEMP "pes3-eject-test-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).log"
$promptedFile = Join-Path $env:TEMP "pes3-eject-prompted-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).json"
Set-Pes3DiscRuntimeOptions -LogPath $log -PromptedVolumesPath $promptedFile -LockLogPath -LockPromptedVolumesPath
$Script:ConfigPath = Join-Path $env:TEMP 'pes3-eject-config.json'
@{ ScanDelaySeconds = 0 } | ConvertTo-Json | Set-Content $Script:ConfigPath
Clear-ConfigCache

Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diy) -NonInteractive
Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diy) -NonInteractive
$c1 = ([regex]::Matches((Get-Content $log -Raw), 'Found PS3 game')).Count
Update-DiscScan -RemoveOnly -ClearTestVolumes
Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diy) -NonInteractive
$c2 = ([regex]::Matches((Get-Content $log -Raw), 'Found PS3 game')).Count
Write-Host "Found count before reset dedupe=$c1 after re-insert=$c2"
if ($c1 -ne 1 -or $c2 -ne 2) { exit 1 }
Write-Host 'PASS'
exit 0
