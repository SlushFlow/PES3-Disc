# PES3-Disc backup manager: list and restore snapshots under RPCS3/PES3/backups.
param(
    [switch]$List,
    [string]$TitleId = '',
    [string]$Restore = '',
    [switch]$RestoreSaves,
    [string]$RestoreTo = '',
    [string]$BackupNow = '',
    [string[]]$SourceDir = @()
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Ps3DiscRun.ps1')

if (-not $List -and -not $Restore -and -not $BackupNow) {
    $List = $true
}

if ($BackupNow) {
    $eboot = $BackupNow
    if (-not (Test-Path -LiteralPath $eboot)) {
        $found = Get-ChildItem -LiteralPath $BackupNow -Recurse -File -Filter 'EBOOT.BIN' -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($found) { $eboot = $found.FullName }
    }
    if (-not (Test-Path -LiteralPath $eboot)) {
        Write-Error "EBOOT not found: $BackupNow"
    }
    $dir = if ($SourceDir.Count -gt 0) {
        @($SourceDir)
    }
    else {
        $meta = Get-GameMetadataFromEboot -EbootPath $eboot
        if ($meta.GameRoot) { @($meta.GameRoot) } else { @() }
    }
    $result = Invoke-Pes3Backup -EbootPath $eboot -SourceDirs $dir -Reason 'manual'
    if ($result) {
        Write-Host "Backup created: $result"
        exit 0
    }
    Write-Error 'Backup failed (see disc-run.log).'
}

if ($List) {
    $items = Get-Pes3BackupList -TitleId $TitleId
    if ($items.Count -eq 0) {
        Write-Host "No backups in: $(Get-BackupRoot)"
        exit 0
    }
    $items | Format-Table -AutoSize TitleId, Snapshot, Title, FileCount, HasSaves, Created, Path
    exit 0
}

if ($Restore) {
    $snapPath = $Restore
    if (-not (Test-Path -LiteralPath $snapPath)) {
        $root = Get-BackupRoot
        if (-not $TitleId) {
            Write-Error 'Use -TitleId with -Restore when passing a snapshot folder name only.'
        }
        $snapPath = Join-Path (Join-Path $root $TitleId) $Restore
    }
    if (-not (Test-Path -LiteralPath $snapPath)) {
        Write-Error "Backup snapshot not found: $snapPath"
    }
    $dest = Restore-Pes3Backup -BackupSnapshotPath $snapPath -RestoreGameTo $RestoreTo -RestoreSaves:$RestoreSaves
    Write-Host "Restored game files to: $dest"
    if ($RestoreSaves) { Write-Host 'Save data restored to dev_hdd0 (previous folder renamed with .before-restore timestamp).' }
    exit 0
}
