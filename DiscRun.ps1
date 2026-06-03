#Requires -Version 5.1
<#
.SYNOPSIS
  PES3-Disc: watch for PS3 discs and prompt to run in RPCS3.
  Modes: default = background watcher; -Scan = one-shot drive scan; -RemoveOnly = eject handling.
#>
param(
    [switch]$Scan,
    [switch]$RemoveOnly,
    [string[]]$TestVolume,
    [switch]$NonInteractive,
    [switch]$ClearTestVolumes
)

$ErrorActionPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'Ps3DiscRun.ps1')

if ($Scan -or $RemoveOnly) {
    $lockFile = Join-Path $env:TEMP 'pes3-disc-scan.lock'
    if (-not $RemoveOnly -and (Test-Path -LiteralPath $lockFile)) {
        $lockAge = (Get-Date) - (Get-Item -LiteralPath $lockFile).LastWriteTime
        if ($lockAge.TotalSeconds -lt 12) {
            Write-Log 'Scan skipped (another scan in progress)'
            exit 0
        }
    }

    if (-not $RemoveOnly) {
        New-Item -ItemType File -Path $lockFile -Force | Out-Null
    }

    try {
        $config = Get-Config
        $delay = 3
        if ($config -and $config.ScanDelaySeconds) {
            $delay = [int]$config.ScanDelaySeconds
        }
        Update-DiscScan -DelaySeconds $(if ($RemoveOnly -or $NonInteractive) { 0 } else { $delay }) -RemoveOnly:$RemoveOnly `
            -TestVolumeRoots $TestVolume -NonInteractive:$NonInteractive -ClearTestVolumes:$ClearTestVolumes
    }
    finally {
        if (-not $RemoveOnly) {
            Remove-Item -LiteralPath $lockFile -Force -ErrorAction SilentlyContinue
        }
    }
    exit 0
}

$mutex = New-Object System.Threading.Mutex($false, 'Global\PES3Disc_Watcher')
if (-not $mutex.WaitOne(0, $false)) {
    Write-Log 'Another PES3-Disc watcher is already running; exiting.'
    exit 0
}

Write-Log 'PES3-Disc started'
[void](Initialize-Pes3DataPaths)
if ($Script:Pes3Root) {
    Write-Log "PES3 data folder: $Script:Pes3Root"
}

$config = Get-Config
if (-not $config -or -not $config.Rpcs3Path -or -not (Test-Path -LiteralPath $config.Rpcs3Path)) {
    $found = Find-Rpcs3Executable
    if ($found) {
        Save-Config -Rpcs3Path $found
        Write-Log "Auto-detected RPCS3: $found"
        $config = Get-Config
    }
}

$scanDelay = 3
if ($config -and $config.ScanDelaySeconds) {
    $scanDelay = [int]$config.ScanDelaySeconds
}

$discRun = Join-Path $PSScriptRoot 'DiscRun.ps1'
Start-Process -FilePath 'powershell.exe' `
    -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden', '-File', "`"$discRun`"", '-Scan') `
    -WindowStyle Hidden | Out-Null

$root = $PSScriptRoot
$queryInsert = "SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 2"
Register-WmiEvent -Query $queryInsert -SourceIdentifier 'Ps3DiscInsert' -Action {
    $delay = $using:scanDelay
    if ($delay -lt 1) { $delay = 1 }
    Start-Sleep -Seconds $delay
    $script = Join-Path $using:root 'DiscRun.ps1'
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden',
        '-File', "`"$script`"", '-Scan'
    ) -WindowStyle Hidden | Out-Null
} | Out-Null

$queryRemove = "SELECT * FROM Win32_VolumeChangeEvent WHERE EventType = 3"
Register-WmiEvent -Query $queryRemove -SourceIdentifier 'Ps3DiscRemove' -Action {
    $script = Join-Path $using:root 'DiscRun.ps1'
    Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-WindowStyle', 'Hidden',
        '-File', "`"$script`"", '-Scan', '-RemoveOnly'
    ) -WindowStyle Hidden | Out-Null
} | Out-Null

Write-Log "Watching for disc insert (scan delay ${scanDelay}s)"

try {
    while ($true) {
        Wait-Event -Timeout 86400 | Out-Null
        Get-Event | Remove-Event -ErrorAction SilentlyContinue
    }
}
finally {
    Get-EventSubscriber | Unregister-Event -ErrorAction SilentlyContinue
}
