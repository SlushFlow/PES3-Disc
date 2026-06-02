# Runs a single disc scan (called from the watcher on insert/remove).
param(
    [switch]$RemoveOnly
)

$ErrorActionPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Ps3DiscRun.Common.ps1')

$config = Get-Config
$delay = 3
if ($config -and $config.ScanDelaySeconds) {
    $delay = [int]$config.ScanDelaySeconds
}

Update-DiscScan -DelaySeconds $(if ($RemoveOnly) { 0 } else { $delay }) -RemoveOnly:$RemoveOnly
