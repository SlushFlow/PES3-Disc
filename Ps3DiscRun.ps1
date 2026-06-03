# PES3-Disc core library (config, paths, disc detection, retail decrypt).

# Shared helpers for PES3-Disc (PlayStation Emulation Station 3 Disc)

$Script:Root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Script:ConfigPath = Join-Path $Script:Root 'config.json'
$Script:PromptedVolumesPath = Join-Path $Script:Root 'prompted-volumes.json'
$Script:LogPath = Join-Path $Script:Root 'disc-run.log'
$Script:ConfigCache = $null
$Script:ConfigCacheTime = $null
$Script:WinFormsLoaded = $false
$Script:NonInteractive = $false
$Script:OverrideLogPath = $false
$Script:OverridePromptedVolumesPath = $false

function Write-Log {
    param([string]$Message)
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    $path = $script:LogPath
    if (-not $path) { return }
    try {
        $logDir = Split-Path -LiteralPath $path -Parent
        if ($logDir -and -not (Test-Path -LiteralPath $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
        [System.IO.File]::AppendAllText($path, $line + [Environment]::NewLine, [System.Text.UTF8Encoding]::new($false))
    }
    catch {
        # Last resort for locked paths
        try { Add-Content -LiteralPath $path -Value $line -Encoding UTF8 -ErrorAction Stop } catch { }
    }
}

function Clear-ConfigCache {
    $Script:ConfigCache = $null
    $Script:ConfigCacheTime = $null
}

function Set-Pes3DiscRuntimeOptions {
    param(
        [string]$LogPath = '',
        [string]$PromptedVolumesPath = '',
        [switch]$LockLogPath,
        [switch]$LockPromptedVolumesPath
    )

    if ($LogPath) { $script:LogPath = $LogPath }
    if ($PromptedVolumesPath) { $script:PromptedVolumesPath = $PromptedVolumesPath }
    if ($LockLogPath) { $script:OverrideLogPath = $true }
    if ($LockPromptedVolumesPath) { $script:OverridePromptedVolumesPath = $true }
}

function Get-Pes3LogPath {
    return $script:LogPath
}

function Ensure-WinFormsLoaded {
    if ($Script:WinFormsLoaded) { return }
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing
    $Script:WinFormsLoaded = $true
}

function Get-PromptedVolumes {
    if (-not (Test-Path -LiteralPath $script:PromptedVolumesPath)) {
        return @{}
    }
    try {
        $list = Get-Content -LiteralPath $script:PromptedVolumesPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $hash = @{}
        foreach ($id in $list) { $hash[$id] = $true }
        return $hash
    }
    catch {
        return @{}
    }
}

function Set-PromptedVolumes {
    param([hashtable]$Volumes)
    $list = @($Volumes.Keys)
    $json = if ($list.Count -gt 0) { $list | ConvertTo-Json } else { '[]' }
    $json | Set-Content -LiteralPath $script:PromptedVolumesPath -Encoding UTF8
}

function Get-Config {
    if (-not (Test-Path -LiteralPath $Script:ConfigPath)) {
        return $null
    }
    try {
        $mtime = (Get-Item -LiteralPath $Script:ConfigPath).LastWriteTimeUtc
        if ($Script:ConfigCache -and $Script:ConfigCacheTime -eq $mtime) {
            return $Script:ConfigCache
        }
        $Script:ConfigCache = Get-Content -LiteralPath $Script:ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $Script:ConfigCacheTime = $mtime
        return $Script:ConfigCache
    }
    catch {
        Write-Log "Failed to read config: $_"
        return $null
    }
}

function Find-Rpcs3Executable {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'RPCS3\rpcs3.exe'),
        (Join-Path $env:ProgramFiles 'RPCS3\rpcs3.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'RPCS3\rpcs3.exe'),
        (Join-Path $env:USERPROFILE 'RPCS3\rpcs3.exe'),
        (Join-Path $env:USERPROFILE 'Downloads\RPCS3\rpcs3.exe')
    )
    foreach ($path in $candidates) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            return $path
        }
    }
    return $null
}

# --- PES3 data paths (RPCS3/PES3/) ---
# PES3 data layout under the RPCS3 install directory (RPCS3/PES3/...).

$Script:Pes3Root = $null
$Script:PathsInitialized = $false

function Get-Rpcs3InstallDir {
    $config = Get-Config
    if ($config -and $config.Rpcs3Path -and (Test-Path -LiteralPath $config.Rpcs3Path)) {
        return (Split-Path -LiteralPath $config.Rpcs3Path -Parent)
    }
    $found = Find-Rpcs3Executable
    if ($found) {
        return (Split-Path -LiteralPath $found -Parent)
    }
    return $null
}

function Initialize-Pes3DataPaths {
    if ($Script:PathsInitialized -and $Script:Pes3Root) {
        return $true
    }

    $rpcs3Dir = Get-Rpcs3InstallDir
    if (-not $rpcs3Dir) {
        return $false
    }

    $pes3 = Join-Path $rpcs3Dir 'PES3'
    foreach ($sub in @('cache', 'logs', 'state', 'temp', 'backups')) {
        $p = Join-Path $pes3 $sub
        if (-not (Test-Path -LiteralPath $p)) {
            New-Item -ItemType Directory -Path $p -Force | Out-Null
        }
    }

    $Script:Pes3Root = $pes3
    if (-not $Script:OverrideLogPath) {
        $Script:LogPath = Join-Path $pes3 'logs\disc-run.log'
    }
    if (-not $Script:OverridePromptedVolumesPath) {
        $Script:PromptedVolumesPath = Join-Path $pes3 'state\prompted-volumes.json'
    }
    $Script:PathsInitialized = $true
    Remove-StalePes3CleanupJobs
    return $true
}

function Remove-StalePes3CleanupJobs {
    $cutoff = (Get-Date).AddDays(-2)
    Get-ChildItem -LiteralPath $env:TEMP -Filter 'pes3-cleanup-job-*.json' -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        ForEach-Object {
            Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue
        }
}

function Get-Pes3Root {
    [void](Initialize-Pes3DataPaths)
    return $Script:Pes3Root
}

function Get-DumpCacheRoot {
    $config = Get-Config
    if ($config -and $config.DumpCachePath -and ($config.DumpCachePath.ToString().Trim().Length -gt 0)) {
        $custom = $config.DumpCachePath.ToString().Trim()
        if (-not (Test-Path -LiteralPath $custom)) {
            New-Item -ItemType Directory -Path $custom -Force | Out-Null
        }
        return $custom
    }

    if (Initialize-Pes3DataPaths) {
        return (Join-Path $Script:Pes3Root 'cache')
    }

    $fallback = Join-Path $env:LOCALAPPDATA 'PES3-Disc\cache'
    if (-not (Test-Path -LiteralPath $fallback)) {
        New-Item -ItemType Directory -Path $fallback -Force | Out-Null
    }
    return $fallback
}

function Test-DeleteCacheAfterPlay {
    $config = Get-Config
    if ($config -and $null -ne $config.DeleteCacheAfterPlay) {
        return [bool]$config.DeleteCacheAfterPlay
    }
    return $true
}

function Get-EphemeralSessionDir {
    $tempRoot = if (Initialize-Pes3DataPaths) {
        Join-Path $Script:Pes3Root 'temp'
    }
    else {
        Join-Path $env:TEMP 'PES3-Disc-sessions'
    }
    if (-not (Test-Path -LiteralPath $tempRoot)) {
        New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    }
    $dir = Join-Path $tempRoot ("session-{0}" -f [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
    return $dir
}

function Register-EphemeralCacheCleanup {
    param(
        [int]$ProcessId,
        [string[]]$CleanupDirs
    )

    $dirs = @($CleanupDirs | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Sort-Object -Unique -Descending)
    if ($dirs.Count -eq 0) { return }

    $jobFile = Join-Path $env:TEMP ("pes3-cleanup-job-{0}.json" -f [Guid]::NewGuid().ToString('N'))
    $job = @{
        ProcessId   = $ProcessId
        CleanupDirs = $dirs
        LibraryPath = (Join-Path $Script:Root 'Ps3DiscRun.ps1')
    }
    ($job | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $jobFile -Encoding UTF8

    $jobFileEscaped = $jobFile.Replace("'", "''")
    $libEscaped = $job.LibraryPath.Replace("'", "''")
    $cleanupCmd = @"
`$ErrorActionPreference = 'SilentlyContinue'
. '$libEscaped'
`$job = Get-Content -LiteralPath '$jobFileEscaped' -Raw -Encoding UTF8 | ConvertFrom-Json
Wait-Pes3SessionEnd -ProcessId `$job.ProcessId
foreach (`$d in `$job.CleanupDirs) {
    if (`$d -and (Test-Path -LiteralPath `$d)) {
        Remove-Item -LiteralPath `$d -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Remove-Item -LiteralPath '$jobFileEscaped' -Force -ErrorAction SilentlyContinue
"@

    Write-Log "Scheduled cache cleanup after RPCS3 (PID $ProcessId): $($dirs -join '; ')"
    Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $cleanupCmd
    ) | Out-Null
}

function Wait-Pes3SessionEnd {
    param(
        [int]$ProcessId,
        [int]$GraceSeconds = 6,
        [int]$MaxWaitHours = 18
    )

    if ($ProcessId -le 0) {
        Start-Sleep -Seconds $GraceSeconds
        return
    }

    $deadline = (Get-Date).AddHours($MaxWaitHours)
    try {
        Wait-Process -Id $ProcessId -ErrorAction Stop
    }
    catch {
        # Process may have already exited before we waited.
    }

    while ((Get-Date) -lt $deadline) {
        $alive = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $alive) { break }
        Start-Sleep -Seconds 2
    }

    Start-Sleep -Seconds $GraceSeconds
}

function Remove-EphemeralCacheDirs {
    param(
        [string[]]$CleanupDirs,
        [string]$EbootPath = '',
        [hashtable]$Game = $null,
        [switch]$SkipBackup
    )

    if (-not $SkipBackup -and $EbootPath -and (Test-BackupsEnabled)) {
        Invoke-Pes3Backup -EbootPath $EbootPath -SourceDirs $CleanupDirs -Game $Game -Reason 'declined_or_cleanup'
    }

    foreach ($dir in ($CleanupDirs | Sort-Object -Unique -Descending)) {
        if (-not $dir -or -not (Test-Path -LiteralPath $dir)) { continue }
        try {
            Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop
            Write-Log "Removed ephemeral cache: $dir"
        }
        catch {
            Write-Log "Failed to remove ephemeral cache ${dir}: $_"
        }
    }
}

function Get-PathsToCleanupForGame {
    param([hashtable]$Game)

    if (-not $Game.EphemeralCleanupDirs) {
        return @()
    }
    return @($Game.EphemeralCleanupDirs)
}

# --- Backups (RPCS3/PES3/backups) ---

function Test-BackupsEnabled {
    $config = Get-Config
    if ($config -and $null -ne $config.EnableBackups) {
        return [bool]$config.EnableBackups
    }
    return $true
}

function Test-BackupSaves {
    $config = Get-Config
    if ($config -and $null -ne $config.BackupSaves) {
        return [bool]$config.BackupSaves
    }
    return $true
}

function Get-MaxBackupsPerTitle {
    $config = Get-Config
    if ($config -and $config.MaxBackupsPerTitle) {
        return [Math]::Max(1, [int]$config.MaxBackupsPerTitle)
    }
    return 3
}

function Get-BackupRoot {
    $config = Get-Config
    if ($config -and $config.BackupPath -and ($config.BackupPath.ToString().Trim().Length -gt 0)) {
        $custom = $config.BackupPath.ToString().Trim()
        if (-not (Test-Path -LiteralPath $custom)) {
            New-Item -ItemType Directory -Path $custom -Force | Out-Null
        }
        return $custom
    }
    if (Initialize-Pes3DataPaths) {
        return (Join-Path $Script:Pes3Root 'backups')
    }
    return (Join-Path $env:LOCALAPPDATA 'PES3-Disc\backups')
}

function Read-ParamSfoValue {
    param(
        [string]$SfoPath,
        [string]$KeyName
    )
    if (-not (Test-Path -LiteralPath $SfoPath)) { return $null }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($SfoPath)
        if ($bytes.Length -lt 20) { return $null }
        if ($bytes[0] -ne 0 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x53 -or $bytes[3] -ne 0x46) {
            return $null
        }

        $keyCount = [BitConverter]::ToUInt32($bytes, 8)
        $keyTableOff = [BitConverter]::ToUInt32($bytes, 12)
        $dataTableOff = [BitConverter]::ToUInt32($bytes, 16)

        for ($i = 0; $i -lt $keyCount; $i++) {
            $entryOff = $keyTableOff + ($i * 16)
            if ($entryOff + 16 -gt $bytes.Length) { break }

            $nameOff = [BitConverter]::ToUInt16($bytes, $entryOff)
            $dataLen = [BitConverter]::ToUInt32($bytes, $entryOff + 4)
            $dataOff = [BitConverter]::ToUInt32($bytes, $entryOff + 12)

            $nameEnd = $nameOff
            while ($nameEnd -lt $bytes.Length -and $bytes[$nameEnd] -ne 0) { $nameEnd++ }
            $key = [System.Text.Encoding]::ASCII.GetString($bytes, $nameOff, $nameEnd - $nameOff)

            if ($key -eq $KeyName) {
                $absOff = $dataTableOff + $dataOff
                if ($absOff + $dataLen -gt $bytes.Length) { return $null }
                $raw = $bytes[$absOff..($absOff + $dataLen - 1)]
                return [System.Text.Encoding]::UTF8.GetString($raw).Trim([char]0)
            }
        }
    }
    catch {
        Write-Log "PARAM.SFO read failed ($SfoPath): $_"
    }
    return $null
}

function Read-ParamSfoFields {
    param([string]$SfoPath)

    $result = @{ TITLE_ID = $null; TITLE = $null }
    if (-not (Test-Path -LiteralPath $SfoPath)) { return $result }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($SfoPath)
        if ($bytes.Length -lt 20) { return $result }
        if ($bytes[0] -ne 0 -or $bytes[1] -ne 0x50 -or $bytes[2] -ne 0x53 -or $bytes[3] -ne 0x46) {
            return $result
        }

        $keyCount = [BitConverter]::ToUInt32($bytes, 8)
        $keyTableOff = [BitConverter]::ToUInt32($bytes, 12)
        $dataTableOff = [BitConverter]::ToUInt32($bytes, 16)

        for ($i = 0; $i -lt $keyCount; $i++) {
            $entryOff = $keyTableOff + ($i * 16)
            if ($entryOff + 16 -gt $bytes.Length) { break }

            $nameOff = [BitConverter]::ToUInt16($bytes, $entryOff)
            $dataLen = [BitConverter]::ToUInt32($bytes, $entryOff + 4)
            $dataOff = [BitConverter]::ToUInt32($bytes, $entryOff + 12)

            $nameEnd = $nameOff
            while ($nameEnd -lt $bytes.Length -and $bytes[$nameEnd] -ne 0) { $nameEnd++ }
            $key = [System.Text.Encoding]::ASCII.GetString($bytes, $nameOff, $nameEnd - $nameOff)

            if ($key -eq 'TITLE_ID' -or $key -eq 'TITLE') {
                $absOff = $dataTableOff + $dataOff
                if ($absOff + $dataLen -le $bytes.Length) {
                    $raw = $bytes[$absOff..($absOff + $dataLen - 1)]
                    $text = [System.Text.Encoding]::UTF8.GetString($raw).Trim([char]0)
                    if ($text) { $result[$key] = $text }
                }
            }
            if ($result.TITLE_ID -and $result.TITLE) { break }
        }
    }
    catch {
        Write-Log "PARAM.SFO read failed ($SfoPath): $_"
    }
    return $result
}

function Get-GameMetadataFromEboot {
    param([string]$EbootPath)

    if (-not $EbootPath -or -not (Test-Path -LiteralPath $EbootPath)) {
        return @{ TitleId = 'UNKNOWN'; Title = 'PS3 game'; GameRoot = $null; SfoPath = $null }
    }

    $ps3Game = Split-Path (Split-Path $EbootPath -Parent) -Parent
    $gameRoot = Split-Path $ps3Game -Parent
    $sfo = Join-Path $ps3Game 'PARAM.SFO'
    $sfoFields = Read-ParamSfoFields -SfoPath $sfo
    $titleId = $sfoFields.TITLE_ID
    $title = $sfoFields.TITLE

    if (-not $titleId) {
        $titleId = Split-Path $gameRoot -Leaf
    }
    if (-not $title) {
        $title = $titleId
    }

    $safeId = ($titleId -replace '[\\/:*?"<>|]', '_').Trim()
    if (-not $safeId) { $safeId = 'UNKNOWN' }

    return @{
        TitleId  = $safeId
        Title    = $title
        GameRoot = $gameRoot
        SfoPath  = $sfo
    }
}

function Copy-DirectoryTree {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) { return $false }
    if (-not (Test-Path -LiteralPath $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }

    if (Get-Command robocopy -ErrorAction SilentlyContinue) {
        $null = robocopy $Source $Destination /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NJH /NJS /nc /ns /np 2>&1
        # Robocopy: 0-7 = success or acceptable (extra files, etc.)
        if ($LASTEXITCODE -ge 8) {
            Write-Log "robocopy failed ($Source -> $Destination) exit $LASTEXITCODE"
            return $false
        }
        return $true
    }

    try {
        Copy-Item -LiteralPath (Join-Path $Source '*') -Destination $Destination -Recurse -Force -ErrorAction Stop
        return $true
    }
    catch {
        Write-Log "Copy-Item failed ($Source): $_"
        return $false
    }
}

function Get-FileSha256 {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Resolve-GameRootsToBackup {
    param([string[]]$SourceDirs)

    $roots = @()
    foreach ($dir in $SourceDirs) {
        if (-not $dir -or -not (Test-Path -LiteralPath $dir)) { continue }
        $literal = (Resolve-Path -LiteralPath $dir).Path
        if (Test-Path -LiteralPath (Join-Path $literal 'PS3_GAME')) {
            $roots += $literal
            continue
        }
        if ((Split-Path $literal -Leaf) -eq 'PS3_GAME') {
            $roots += (Split-Path $literal -Parent)
            continue
        }
        $roots += $literal
    }
    return @($roots | Select-Object -Unique)
}

function Remove-OldBackupsForTitle {
    param([string]$TitleId)

    $titleDir = Join-Path (Get-BackupRoot) $TitleId
    if (-not (Test-Path -LiteralPath $titleDir)) { return }

    $keep = Get-MaxBackupsPerTitle
    $existing = Get-ChildItem -LiteralPath $titleDir -Directory | Sort-Object Name -Descending
    foreach ($old in $existing | Select-Object -Skip $keep) {
        try {
            Remove-Item -LiteralPath $old.FullName -Recurse -Force -ErrorAction Stop
            Write-Log "Pruned old backup: $($old.FullName)"
        }
        catch {
            Write-Log "Failed to prune backup $($old.FullName): $_"
        }
    }
}

function Test-RecentBackupExists {
    param(
        [string]$TitleId,
        [string]$Reason,
        [int]$WindowSeconds = 120
    )

    if (-not $TitleId) { return $false }
    $titleDir = Join-Path (Get-BackupRoot) $TitleId
    if (-not (Test-Path -LiteralPath $titleDir)) { return $false }

    $latest = Get-ChildItem -LiteralPath $titleDir -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending |
        Select-Object -First 1
    if (-not $latest) { return $false }

    $manifestPath = Join-Path $latest.FullName 'manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { return $false }

    try {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($manifest.reason -ne $Reason) { return $false }
        $created = [datetime]::Parse($manifest.created)
        return ((Get-Date) - $created).TotalSeconds -lt $WindowSeconds
    }
    catch {
        return $false
    }
}

function Invoke-Pes3Backup {
    param(
        [string]$EbootPath,
        [string[]]$SourceDirs = @(),
        [hashtable]$Game = $null,
        [string]$Reason = 'manual'
    )

    if (-not (Test-BackupsEnabled)) { return $null }
    if (-not $EbootPath) { return $null }

    [void](Initialize-Pes3DataPaths)

    $meta = Get-GameMetadataFromEboot -EbootPath $EbootPath
    if ((Test-RecentBackupExists -TitleId $meta.TitleId -Reason $Reason)) {
        Write-Log "Backup skipped (recent $Reason snapshot for $($meta.TitleId))"
        $titleDir = Join-Path (Get-BackupRoot) $meta.TitleId
        $latest = Get-ChildItem -LiteralPath $titleDir -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First 1
        return if ($latest) { $latest.FullName } else { $null }
    }
    $roots = Resolve-GameRootsToBackup -SourceDirs $SourceDirs
    if ($meta.GameRoot -and ($roots -notcontains $meta.GameRoot)) {
        $roots = @($meta.GameRoot) + $roots
    }
    $roots = @($roots | Select-Object -Unique)
    if ($roots.Count -eq 0) { return $null }

    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $backupDir = Join-Path (Join-Path (Get-BackupRoot) $meta.TitleId) $stamp
    $gameBackupDir = Join-Path $backupDir 'game'
    New-Item -ItemType Directory -Path $gameBackupDir -Force | Out-Null

    $copiedRoots = @()
    foreach ($root in $roots) {
        $leaf = Split-Path $root -Leaf
        $dest = Join-Path $gameBackupDir $leaf
        if (Copy-DirectoryTree -Source $root -Destination $dest) {
            $copiedRoots += $root
        }
    }

    if ($copiedRoots.Count -eq 0) {
        Write-Log "Backup failed: no files copied for $($meta.TitleId)"
        return $null
    }

    $ebootInBackup = $null
    foreach ($root in $copiedRoots) {
        $leaf = Split-Path $root -Leaf
        $candidate = Join-Path (Join-Path $gameBackupDir $leaf) 'PS3_GAME\USRDIR\EBOOT.BIN'
        if (Test-Path -LiteralPath $candidate) {
            $ebootInBackup = Get-Item -LiteralPath $candidate
            break
        }
    }
    if (-not $ebootInBackup) {
        $ebootInBackup = Get-ChildItem -LiteralPath $gameBackupDir -Recurse -File -Filter 'EBOOT.BIN' -ErrorAction SilentlyContinue |
            Select-Object -First 1
    }
    $fileCount = -1
    try {
        $fileCount = (Get-ChildItem -LiteralPath $gameBackupDir -Recurse -File -ErrorAction Stop | Measure-Object).Count
    }
    catch {
        Write-Log "Backup file count skipped for $($meta.TitleId): $_"
    }

    $savedataBackedUp = $false
    if ((Test-BackupSaves) -and $meta.TitleId -ne 'UNKNOWN') {
        $rpcs3Dir = Get-Rpcs3InstallDir
        $saveSrc = if ($rpcs3Dir) { Join-Path $rpcs3Dir "dev_hdd0\savedata\$($meta.TitleId)" } else { $null }
        if ($saveSrc -and (Test-Path -LiteralPath $saveSrc)) {
            $saveDest = Join-Path $backupDir 'savedata'
            $savedataBackedUp = Copy-DirectoryTree -Source $saveSrc -Destination $saveDest
        }
    }

    $manifest = [ordered]@{
        created       = (Get-Date).ToString('o')
        reason        = $Reason
        titleId       = $meta.TitleId
        title         = $meta.Title
        ebootPath     = $EbootPath
        sourceRoots   = $copiedRoots
        fileCount     = $fileCount
        ebootSha256   = if ($ebootInBackup) { Get-FileSha256 -Path $ebootInBackup.FullName } else { $null }
        includesSaves = $savedataBackedUp
    }
    ($manifest | ConvertTo-Json -Depth 5) | Set-Content -LiteralPath (Join-Path $backupDir 'manifest.json') -Encoding UTF8

    Remove-OldBackupsForTitle -TitleId $meta.TitleId
    Write-Log "Backup created: $backupDir ($($meta.TitleId), $fileCount files, saves=$savedataBackedUp)"
    return $backupDir
}

function Get-Pes3BackupList {
    param([string]$TitleId = '')

    $root = Get-BackupRoot
    if (-not (Test-Path -LiteralPath $root)) { return @() }

    $results = @()
    $titleDirs = if ($TitleId) {
        @(Get-Item -LiteralPath (Join-Path $root $TitleId) -ErrorAction SilentlyContinue)
    }
    else {
        Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue
    }

    foreach ($td in $titleDirs) {
        if (-not $td) { continue }
        foreach ($snap in Get-ChildItem -LiteralPath $td.FullName -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending) {
            $manifestPath = Join-Path $snap.FullName 'manifest.json'
            $manifest = $null
            if (Test-Path -LiteralPath $manifestPath) {
                try { $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json } catch { }
            }
            $results += [PSCustomObject]@{
                TitleId   = $td.Name
                Snapshot  = $snap.Name
                Path      = $snap.FullName
                Created   = if ($manifest.created) { $manifest.created } else { $snap.Name }
                Title     = if ($manifest.title) { $manifest.title } else { '' }
                FileCount = if ($manifest.fileCount) { $manifest.fileCount } else { 0 }
                HasSaves  = if ($manifest.includesSaves) { $manifest.includesSaves } else { $false }
            }
        }
    }
    return $results
}

function Restore-Pes3Backup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BackupSnapshotPath,
        [string]$RestoreGameTo = '',
        [switch]$RestoreSaves
    )

    if (-not (Test-Path -LiteralPath $BackupSnapshotPath)) {
        throw "Backup not found: $BackupSnapshotPath"
    }

    $manifestPath = Join-Path $BackupSnapshotPath 'manifest.json'
    $manifest = $null
    if (Test-Path -LiteralPath $manifestPath) {
        $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }

    $gameSrc = Join-Path $BackupSnapshotPath 'game'
    if (-not (Test-Path -LiteralPath $gameSrc)) {
        throw 'Backup is missing game/ folder.'
    }

    if (-not $RestoreGameTo) {
        if ($manifest -and $manifest.titleId) {
            $RestoreGameTo = Join-Path (Get-DumpCacheRoot) $manifest.titleId
        }
        else {
            $RestoreGameTo = Join-Path (Get-DumpCacheRoot) ('restored-' + (Split-Path $BackupSnapshotPath -Leaf))
        }
    }

    if (Test-Path -LiteralPath $RestoreGameTo) {
        $archive = "$RestoreGameTo.before-restore.{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss')
        Move-Item -LiteralPath $RestoreGameTo -Destination $archive -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $RestoreGameTo -Force | Out-Null

    $children = Get-ChildItem -LiteralPath $gameSrc -ErrorAction SilentlyContinue
    if ($children.Count -eq 1 -and $children[0].PSIsContainer) {
        Copy-DirectoryTree -Source $children[0].FullName -Destination $RestoreGameTo | Out-Null
    }
    else {
        Copy-DirectoryTree -Source $gameSrc -Destination $RestoreGameTo | Out-Null
    }

    if ($RestoreSaves -and $manifest -and $manifest.titleId) {
        $saveSrc = Join-Path $BackupSnapshotPath 'savedata'
        if (Test-Path -LiteralPath $saveSrc) {
            $rpcs3Dir = Get-Rpcs3InstallDir
            if ($rpcs3Dir) {
                $saveDest = Join-Path $rpcs3Dir "dev_hdd0\savedata\$($manifest.titleId)"
                if (Test-Path -LiteralPath $saveDest) {
                    $saveArchive = "$saveDest.before-restore.{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss')
                    Move-Item -LiteralPath $saveDest -Destination $saveArchive -Force -ErrorAction SilentlyContinue
                }
                Copy-DirectoryTree -Source $saveSrc -Destination $saveDest | Out-Null
            }
        }
    }

    Write-Log "Restored backup to $RestoreGameTo (saves=$RestoreSaves)"
    return $RestoreGameTo
}

function Save-Config {
    param(
        [string]$Rpcs3Path,
        [int]$ScanDelaySeconds = 3,
        [bool]$UseNoGui = $false
    )
    $existing = Get-Config
    $obj = [ordered]@{
        Rpcs3Path                    = $Rpcs3Path
        ScanDelaySeconds             = $ScanDelaySeconds
        UseNoGui                     = $UseNoGui
        EnableRetailDecrypt          = if ($existing -and $null -ne $existing.EnableRetailDecrypt) { $existing.EnableRetailDecrypt } else { $true }
        DecryptUnknownOpticalMedia   = if ($existing -and $null -ne $existing.DecryptUnknownOpticalMedia) { $existing.DecryptUnknownOpticalMedia } else { $false }
        DeleteCacheAfterPlay         = if ($existing -and $null -ne $existing.DeleteCacheAfterPlay) { $existing.DeleteCacheAfterPlay } else { $true }
        DumpCachePath                = if ($existing -and $existing.DumpCachePath) { $existing.DumpCachePath } else { '' }
        DumpCliPath                  = if ($existing -and $existing.DumpCliPath) { $existing.DumpCliPath } else { '' }
        EnableBackups                = if ($existing -and $null -ne $existing.EnableBackups) { $existing.EnableBackups } else { $true }
        BackupSaves                  = if ($existing -and $null -ne $existing.BackupSaves) { $existing.BackupSaves } else { $true }
        BackupOnLaunch               = if ($existing -and $null -ne $existing.BackupOnLaunch) { $existing.BackupOnLaunch } else { $false }
        MaxBackupsPerTitle           = if ($existing -and $existing.MaxBackupsPerTitle) { [int]$existing.MaxBackupsPerTitle } else { 3 }
        BackupPath                   = if ($existing -and $existing.BackupPath) { $existing.BackupPath } else { '' }
    }
    ($obj | ConvertTo-Json -Depth 4) | Set-Content -LiteralPath $Script:ConfigPath -Encoding UTF8
    Clear-ConfigCache
    $Script:PathsInitialized = $false
    [void](Initialize-Pes3DataPaths)
}

function Read-ParamSfoTitle {
    param([string]$SfoPath)
    return Read-ParamSfoValue -SfoPath $SfoPath -KeyName 'TITLE'
}

function Test-BackupOnLaunch {
    $config = Get-Config
    if ($config -and $null -ne $config.BackupOnLaunch) {
        return [bool]$config.BackupOnLaunch
    }
    return $false
}

function Test-PathEboot {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) { return $Path }
    # Windows is usually case-insensitive; explicit check for odd ISO mounts
    $dir = Split-Path $Path -Parent
    $file = Split-Path $Path -Leaf
    if (-not (Test-Path -LiteralPath $dir)) { return $null }
    $match = Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq $file }
    if ($match) { return $match.FullName }
    return $null
}

function Get-Ps3GameFromEbootPath {
    param([string]$EbootPath)
    $meta = Get-GameMetadataFromEboot -EbootPath $EbootPath
    return @{
        Eboot                 = $EbootPath
        Title                 = $meta.Title
        TitleId               = $meta.TitleId
        GameRoot              = $meta.GameRoot
        Ps3GameDir            = Join-Path $meta.GameRoot 'PS3_GAME'
        EphemeralCleanupDirs  = @()
    }
}

function Find-Ps3GameOnDrive {
    param([string]$DriveRoot)

    if (-not $DriveRoot.EndsWith('\')) {
        $DriveRoot = $DriveRoot + '\'
    }

    $layouts = @(
        (Join-Path $DriveRoot 'PS3_GAME\USRDIR\EBOOT.BIN'),
        (Join-Path $DriveRoot 'dev_bdvd\PS3_GAME\USRDIR\EBOOT.BIN')
    )

    foreach ($candidate in $layouts) {
        $eboot = Test-PathEboot -Path $candidate
        if ($eboot) {
            return Get-Ps3GameFromEbootPath -EbootPath $eboot
        }
    }

    try {
        $subdirs = @(Get-ChildItem -LiteralPath $DriveRoot -Directory -ErrorAction SilentlyContinue | Select-Object -First 48)
        foreach ($dir in $subdirs) {
            $eboot = Test-PathEboot -Path (Join-Path $dir.FullName 'PS3_GAME\USRDIR\EBOOT.BIN')
            if ($eboot) {
                return Get-Ps3GameFromEbootPath -EbootPath $eboot
            }
        }
    }
    catch { }

    return $null
}


function Test-EncryptedPs3Eboot {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    try {
        $buf = New-Object byte[] 7
        $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        try {
            $read = $fs.Read($buf, 0, 7)
            if ($read -lt 7) { return $false }
        }
        finally {
            $fs.Dispose()
        }
        return ($buf[0] -eq 0x53 -and $buf[1] -eq 0x43 -and $buf[2] -eq 0x45 -and $buf[6] -eq 2)
    }
    catch {
        return $false
    }
}

function Test-HasPs3DiscMarker {
    param([string]$DriveRoot)
    if (-not $DriveRoot.EndsWith('\')) { $DriveRoot = $DriveRoot + '\' }
    return (Test-Path -LiteralPath (Join-Path $DriveRoot 'PS3_DISC.SFB'))
}

# Classifies what Windows can see on a volume (used for logging / future prompts).
function Get-Ps3DiscVolumeStatus {
    param([string]$DriveRoot)

    if (-not $DriveRoot.EndsWith('\')) { $DriveRoot = $DriveRoot + '\' }

    $ebootPath = Test-PathEboot -Path (Join-Path $DriveRoot 'PS3_GAME\USRDIR\EBOOT.BIN')
    $paramSfo = Join-Path $DriveRoot 'PS3_GAME\PARAM.SFO'
    $hasParam = Test-Path -LiteralPath $paramSfo
    $hasSfb = Test-HasPs3DiscMarker -DriveRoot $DriveRoot

    if ($ebootPath) {
        if (Test-EncryptedPs3Eboot -Path $ebootPath) {
            return @{
                Kind    = 'EncryptedRetail'
                Message = 'Encrypted retail EBOOT.BIN detected; decryption required before RPCS3 can run.'
                Game    = $null
            }
        }
        $game = Get-Ps3GameFromEbootPath -EbootPath $ebootPath
        return @{
            Kind    = 'Playable'
            Message = 'Decrypted PS3_GAME layout with EBOOT.BIN found.'
            Game    = $game
        }
    }

    $ps3GameDir = Join-Path $DriveRoot 'PS3_GAME'
    if ((Test-Path -LiteralPath $ps3GameDir) -and -not $ebootPath) {
        return @{
            Kind    = 'IncompleteBurn'
            Message = 'PS3_GAME folder present but EBOOT.BIN is missing or unreadable.'
            Game    = $null
        }
    }

    if ($hasParam -or $hasSfb) {
        return @{
            Kind    = 'EncryptedRetail'
            Message = 'Retail PS3 disc structure detected (encrypted or partial mount).'
            Game    = $null
        }
    }

    return @{
        Kind    = 'NoPs3Layout'
        Message = 'No PS3 layout on volume. Official discs may need a compatible BD drive; try retail decrypt if the disc is inserted.'
        Game    = $null
    }
}

function Get-OpticalDrives {
    # CI non-interactive runs must not poll real drives (Get-Volume per letter can hang minutes).
    if ($env:GITHUB_ACTIONS -eq 'true' -and $Script:NonInteractive) {
        return @()
    }

    $drives = @()
    try {
        $disks = Get-CimInstance -ClassName Win32_LogicalDisk -Filter 'DriveType=5' -ErrorAction Stop
        foreach ($disk in $disks) {
            if (-not $disk.DeviceID -or -not $disk.FileSystem -or $disk.Size -le 0) { continue }
            $letter = $disk.DeviceID.Substring(0, 1)
            $root = '{0}:\' -f $letter
            $id = if ($disk.VolumeSerialNumber) {
                "$letter|$($disk.VolumeSerialNumber)"
            }
            else {
                "$letter|$($disk.VolumeName)"
            }
            $drives += @{
                Letter = [char]$letter
                Root   = $root
                Id     = $id
            }
        }
        if ($drives.Count -gt 0) { return [object[]]$drives }
    }
    catch { }

    foreach ($letter in [char[]](65..90)) {
        $root = '{0}:\' -f $letter
        if (-not (Test-Path -LiteralPath $root)) { continue }
        try {
            $vol = Get-Volume -DriveLetter $letter -ErrorAction Stop
            if ($vol.DriveType -eq 'CD-ROM' -and $vol.FileSystem -and $vol.Size -gt 0) {
                $id = if ($vol.UniqueId) { $vol.UniqueId.ToString() } else { "$letter|$($vol.FileSystemLabel)" }
                $drives += @{
                    Letter = $letter
                    Root   = $root
                    Id     = $id
                }
            }
        }
        catch { }
    }
    return [object[]]$drives
}

function Start-Rpcs3Game {
    param(
        [string]$Rpcs3Path,
        [string]$EbootPath,
        [bool]$UseNoGui,
        [string[]]$EphemeralCleanupDirs = @(),
        [hashtable]$Game = $null
    )

    $shouldBackup = (Test-BackupsEnabled) -and (
        ($EphemeralCleanupDirs -and $EphemeralCleanupDirs.Count -gt 0) -or (Test-BackupOnLaunch)
    )
    if ($shouldBackup) {
        Invoke-Pes3Backup -EbootPath $EbootPath -SourceDirs $EphemeralCleanupDirs -Game $Game -Reason 'before_play' | Out-Null
    }

    $argList = @()
    if ($UseNoGui) { $argList += '--no-gui' }
    $argList += $EbootPath

    Write-Log "Launching: $Rpcs3Path $($argList -join ' ')"
    $proc = Start-Process -FilePath $Rpcs3Path -ArgumentList $argList -WorkingDirectory (Split-Path $Rpcs3Path -Parent) -PassThru

    if ($EphemeralCleanupDirs -and $EphemeralCleanupDirs.Count -gt 0 -and $proc) {
        Register-EphemeralCacheCleanup -ProcessId $proc.Id -CleanupDirs $EphemeralCleanupDirs
    }

    return $proc
}

function Get-TestVolumeDrives {
    param([string[]]$Roots)

    $drives = @()
    $index = 0
    foreach ($root in $Roots) {
        if (-not $root) { continue }
        $full = $root.Trim().TrimEnd('\')
        if (-not (Test-Path -LiteralPath $full)) { continue }
        try {
            $full = (Resolve-Path -LiteralPath $full).Path
        }
        catch { }
        if (-not $full.EndsWith('\')) { $full = $full + '\' }
        $leaf = Split-Path $full.TrimEnd('\') -Leaf
        $drives += @{
            Letter = [char](65 + ($index % 26))
            Root   = $full
            Id     = "TESTVOL|$index|$leaf"
        }
        $index++
    }
    return [object[]]$drives
}

function Invoke-DiscPrompt {
    param(
        [hashtable]$Drive,
        [hashtable]$Game,
        [hashtable]$PromptedVolumes
    )

    if ($PromptedVolumes.ContainsKey($Drive.Id)) {
        return $PromptedVolumes
    }
    $PromptedVolumes[$Drive.Id] = $true

    if ($Script:NonInteractive) {
        $title = if ($Game.Title) { $Game.Title } else { 'PS3 game' }
        Write-Log "NonInteractive: playable game on $($Drive.Letter): $title -> $($Game.Eboot)"
        return $PromptedVolumes
    }

    Ensure-WinFormsLoaded

    $config = Get-Config
    $rpcs3 = if ($config) { $config.Rpcs3Path } else { $null }

    if (-not $rpcs3 -or -not (Test-Path -LiteralPath $rpcs3)) {
        $rpcs3 = Find-Rpcs3Executable
        if ($rpcs3) {
            Save-Config -Rpcs3Path $rpcs3
            $config = Get-Config
        }
    }

    if (-not $rpcs3 -or -not (Test-Path -LiteralPath $rpcs3)) {
        $dialog = New-Object System.Windows.Forms.OpenFileDialog
        $dialog.Filter = 'RPCS3 (rpcs3.exe)|rpcs3.exe|All files (*.*)|*.*'
        $dialog.Title = 'Select rpcs3.exe'
        if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
            Write-Log 'User cancelled RPCS3 path selection'
            $PromptedVolumes.Remove($Drive.Id) | Out-Null
            return $PromptedVolumes
        }
        $rpcs3 = $dialog.FileName
        $delay = if ($config -and $config.ScanDelaySeconds) { [int]$config.ScanDelaySeconds } else { 3 }
        $noGui = if ($config -and $null -ne $config.UseNoGui) { [bool]$config.UseNoGui } else { $false }
        Save-Config -Rpcs3Path $rpcs3 -ScanDelaySeconds $delay -UseNoGui $noGui
        $config = Get-Config
    }

    $title = if ($Game.Title) { $Game.Title } else { 'PS3 game' }
    $message = @"
A PlayStation 3 game was detected on drive $($Drive.Letter):.

$title

Run this game in RPCS3 now?
"@

    $result = [System.Windows.Forms.MessageBox]::Show(
        $message,
        'PES3-Disc',
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon]::Question
    )

    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
        try {
            $Game = Prepare-GamePlayCache -Drive $Drive -Game $Game
        }
        catch {
            Write-Log "DIY cache staging failed: $_"
            [void][System.Windows.Forms.MessageBox]::Show(
                "Could not copy the disc to the PES3 cache:`n$_",
                'PES3-Disc',
                [System.Windows.Forms.MessageBoxButtons]::OK,
                [System.Windows.Forms.MessageBoxIcon]::Warning
            )
            $PromptedVolumes.Remove($Drive.Id) | Out-Null
            return $PromptedVolumes
        }

        $cleanupDirs = Get-PathsToCleanupForGame -Game $Game
        $useNoGui = $false
        if ($config -and $null -ne $config.UseNoGui) {
            $useNoGui = [bool]$config.UseNoGui
        }
        Start-Rpcs3Game -Rpcs3Path $rpcs3 -EbootPath $Game.Eboot -UseNoGui $useNoGui -EphemeralCleanupDirs $cleanupDirs -Game $Game
    }
    else {
        Write-Log "User declined: $($Game.Eboot)"
    }

    return $PromptedVolumes
}


# --- Retail disc decryption ---
# Retail (official) PS3 disc decryption via pes3-disc-dump CLI (PS3 Disc Dumper engine).

function Get-DumpCliPath {
    $config = Get-Config
    if ($config -and $config.DumpCliPath -and (Test-Path -LiteralPath $config.DumpCliPath)) {
        return $config.DumpCliPath
    }

    $candidates = @(
        (Join-Path $Script:Root 'tools\pes3-disc-dump.exe'),
        (Join-Path $Script:Root 'tools\PES3-Disc.DumpCli\bin\Release\net10.0\win-x64\pes3-disc-dump.exe'),
        (Join-Path $Script:Root 'tools\PES3-Disc.DumpCli\bin\Release\net10.0\pes3-disc-dump.exe')
    )
    foreach ($p in $candidates) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

function Get-CachedGameDump {
    param(
        [string]$VolumeId,
        [string]$ProductCode,
        [string]$TitleId = ''
    )

    if (Test-DeleteCacheAfterPlay) {
        return $null
    }

    $cacheRoot = Get-DumpCacheRoot
    $search = @()
    foreach ($key in @($ProductCode, $TitleId)) {
        if (-not $key) { continue }
        $safe = ($key -replace '[\\/:*?"<>|]', '_').Trim()
        if ($safe.Length -gt 64) { $safe = $safe.Substring(0, 64) }
        if ($safe) { $search += Join-Path $cacheRoot $safe }
    }
    if ($VolumeId) {
        $safe = ($VolumeId -replace '[\\/:*?"<>|]', '_').Trim()
        if ($safe.Length -gt 64) { $safe = $safe.Substring(0, 64) }
        if ($safe) { $search += Join-Path $cacheRoot $safe }
    }
    $search = @($search | Select-Object -Unique)

    foreach ($dir in $search) {
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        $game = Find-Ps3GameOnDrive -DriveRoot ($dir + '\')
        if ($game) {
            return @{
                CacheDir = $dir
                Game     = $game
            }
        }
        foreach ($sub in Get-ChildItem -LiteralPath $dir -Directory -ErrorAction SilentlyContinue) {
            $game = Find-Ps3GameOnDrive -DriveRoot ($sub.FullName + '\')
            if ($game) {
                return @{
                    CacheDir = $sub.FullName
                    Game     = $game
                }
            }
        }
    }
    return $null
}

function Get-CachedRetailDump {
    param(
        [string]$VolumeId,
        [string]$ProductCode
    )
    return Get-CachedGameDump -VolumeId $VolumeId -ProductCode $ProductCode
}

function Prepare-GamePlayCache {
    param(
        [hashtable]$Drive,
        [hashtable]$Game
    )

    $meta = Get-GameMetadataFromEboot -EbootPath $Game.Eboot
    $titleId = if ($Game.TitleId) { $Game.TitleId } else { $meta.TitleId }

    $cached = Get-CachedGameDump -VolumeId $Drive.Id -ProductCode $null -TitleId $titleId
    if ($cached) {
        Write-Log "Using cached DIY dump: $($cached.Game.Eboot)"
        $Game.Eboot = $cached.Game.Eboot
        $Game.Title = $cached.Game.Title
        $Game.TitleId = $titleId
        $Game.GameRoot = $cached.CacheDir
        $Game.EphemeralCleanupDirs = @()
        return $Game
    }

    $gameRoot = if ($Game.GameRoot) { $Game.GameRoot } else { $meta.GameRoot }
    if (-not $gameRoot -or -not (Test-Path -LiteralPath $gameRoot)) {
        throw "Could not locate game folder on disc for cache staging."
    }

    if (Test-DeleteCacheAfterPlay) {
        $dest = Get-EphemeralSessionDir
        $cleanup = @($dest)
    }
    else {
        $dest = Join-Path (Get-DumpCacheRoot) $titleId
        if (Test-Path -LiteralPath $dest) {
            Remove-Item -LiteralPath $dest -Recurse -Force -ErrorAction SilentlyContinue
        }
        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        $cleanup = @()
    }

    Write-Log "Staging DIY disc to cache: $gameRoot -> $dest"
    if (-not (Copy-DirectoryTree -Source $gameRoot -Destination $dest)) {
        throw "Failed to copy disc files to PES3 cache."
    }

    $staged = Find-Ps3GameOnDrive -DriveRoot ($dest + '\')
    if (-not $staged) {
        throw "EBOOT.BIN not found after staging to cache."
    }

    $Game.Eboot = $staged.Eboot
    $Game.Title = $staged.Title
    $Game.TitleId = $titleId
    $Game.GameRoot = $dest
    $Game.EphemeralCleanupDirs = $cleanup
    Write-Log "DIY cache ready: $($Game.Eboot)"
    return $Game
}

function Show-DecryptProgressForm {
    param(
        [string]$Title,
        [scriptblock]$OnCancel
    )

    Ensure-WinFormsLoaded

    $form = New-Object System.Windows.Forms.Form
    $form.Text = 'PES3-Disc - Decrypting'
    $form.Size = New-Object System.Drawing.Size(480, 180)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.TopMost = $true

    $label = New-Object System.Windows.Forms.Label
    $label.Location = New-Object System.Drawing.Point(16, 16)
    $label.Size = New-Object System.Drawing.Size(440, 48)
    $label.Text = $Title
    $form.Controls.Add($label)

    $bar = New-Object System.Windows.Forms.ProgressBar
    $bar.Location = New-Object System.Drawing.Point(16, 72)
    $bar.Size = New-Object System.Drawing.Size(440, 24)
    $bar.Style = 'Marquee'
    $bar.MarqueeAnimationSpeed = 30
    $form.Controls.Add($bar)

    $detail = New-Object System.Windows.Forms.Label
    $detail.Location = New-Object System.Drawing.Point(16, 104)
    $detail.Size = New-Object System.Drawing.Size(440, 24)
    $detail.Text = 'This may take 30-90 minutes for large games...'
    $form.Controls.Add($detail)

    $cancelBtn = New-Object System.Windows.Forms.Button
    $cancelBtn.Location = New-Object System.Drawing.Point(360, 104)
    $cancelBtn.Size = New-Object System.Drawing.Size(96, 28)
    $cancelBtn.Text = 'Cancel'
    $cancelBtn.Add_Click({
        if ($OnCancel) { & $OnCancel }
        $form.Close()
    })
    $form.Controls.Add($cancelBtn)

    $timer = New-Object System.Windows.Forms.Timer
    $timer.Interval = 500
    $script:progressFile = $null
    $timer.Add_Tick({
        if (-not $script:progressFile -or -not (Test-Path -LiteralPath $script:progressFile)) { return }
        try {
            $json = Get-Content -LiteralPath $script:progressFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($json.Title) {
                $label.Text = "Decrypting: $($json.Title)"
            }
            if ($json.TotalFileSectors -gt 0) {
                $bar.Style = 'Continuous'
                $pct = [int](100 * $json.ProcessedSectors / $json.TotalFileSectors)
                $bar.Value = [Math]::Min(100, [Math]::Max(0, $pct))
                $detail.Text = "File $($json.CurrentFile) of $($json.TotalFiles) - $pct%"
            }
        }
        catch { }
    })

    return @{
        Form         = $form
        Label        = $label
        Detail       = $detail
        ProgressBar  = $bar
        Timer        = $timer
        SetProgressFile = {
            param($path)
            $script:progressFile = $path
            $timer.Start()
        }
    }
}

function Invoke-RetailDiscDecrypt {
    param(
        [hashtable]$Drive,
        [hashtable]$PromptedVolumes
    )

    if ($Script:NonInteractive) {
        if (-not $PromptedVolumes.ContainsKey($Drive.Id)) {
            Write-Log "NonInteractive: retail decrypt path on $($Drive.Letter): ( $($Drive.Root) )"
            $PromptedVolumes[$Drive.Id] = $true
        }
        return $PromptedVolumes
    }

    $dumpCli = Get-DumpCliPath
    if (-not $dumpCli) {
        [void][System.Windows.Forms.MessageBox]::Show(
            @"
Retail disc decryption is not set up yet.

Run Setup.ps1 -RetailDecrypt once to build the decryptor, or set DumpCliPath in config.json.

You need:
- .NET 10 SDK (to build) or a pre-built pes3-disc-dump.exe in tools\
- A compatible Blu-ray drive (see RPCS3 quickstart)
- Enough free disk space for the full game
"@,
            'PES3-Disc',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        )
        return $PromptedVolumes
    }

    $ephemeral = Test-DeleteCacheAfterPlay
    if (-not $ephemeral) {
        $cached = Get-CachedRetailDump -VolumeId $Drive.Id -ProductCode $null
        if ($cached) {
            Write-Log "Using cached retail dump: $($cached.Game.Eboot)"
            return Invoke-DiscPrompt -Drive $Drive -Game $cached.Game -PromptedVolumes $PromptedVolumes
        }
    }

    [void](Initialize-Pes3DataPaths)
    $pes3Note = if ($Script:Pes3Root) { "`nDecrypted files go under: $Script:Pes3Root" } else { '' }
    $ephemeralNote = if ($ephemeral) {
        "`nCache is removed when you close RPCS3 (saves stay in dev_hdd0)."
    } else {
        ''
    }

    Ensure-WinFormsLoaded
    $confirm = [System.Windows.Forms.MessageBox]::Show(
        @"
A retail PS3 disc was detected on drive $($Drive.Letter):.

PES3-Disc can decrypt it and launch RPCS3. This requires:
- A compatible Blu-ray drive (MediaTek chipset - see RPCS3 quickstart)
- PS3_DISC.SFB visible on the disc in Windows (many official discs show at least this)
- An IRD key in the online databases (same as PS3 Disc Dumper)
- Tens of GB free space and a long wait (30-90+ min)$pes3Note$ephemeralNote

Decrypt and play now?
"@,
        'PES3-Disc',
        [System.Windows.Forms.MessageBoxButtons]::YesNo,
        [System.Windows.Forms.MessageBoxIcon].Question
    )
    if ($confirm -ne [System.Windows.Forms.DialogResult]::Yes) {
        return $PromptedVolumes
    }

    if ($ephemeral) {
        $sessionDir = Get-EphemeralSessionDir
    }
    else {
        $cacheRoot = Get-DumpCacheRoot
        if (-not (Test-Path -LiteralPath $cacheRoot)) {
            New-Item -ItemType Directory -Path $cacheRoot -Force | Out-Null
        }
        $sessionDir = Join-Path $cacheRoot ("dump-{0:yyyyMMdd-HHmmss}" -f (Get-Date))
        New-Item -ItemType Directory -Path $sessionDir -Force | Out-Null
    }
    $progressFile = Join-Path $env:TEMP ("pes3-disc-progress-{0}.json" -f [Guid]::NewGuid().ToString('N'))

    $ui = Show-DecryptProgressForm -Title "Decrypting disc on drive $($Drive.Letter):..."
    $ui.SetProgressFile.Invoke($progressFile)

    $stdoutFile = Join-Path $env:TEMP ("pes3-disc-stdout-{0}.txt" -f [Guid]::NewGuid().ToString('N'))
    $stderrFile = Join-Path $env:TEMP ("pes3-disc-stderr-{0}.txt" -f [Guid]::NewGuid().ToString('N'))
    $argList = @(
        '--output', "`"$sessionDir`"",
        '--drive', $Drive.Letter,
        '--progress', "`"$progressFile`""
    )

    $proc = Start-Process -FilePath $dumpCli `
        -ArgumentList $argList `
        -RedirectStandardOutput $stdoutFile `
        -RedirectStandardError $stderrFile `
        -NoNewWindow -PassThru

    [System.Windows.Forms.Application]::EnableVisualStyles()
    $ui.Form.Show()
    $ui.Timer.Start()
    $script:RetailDecryptCancelled = $false
    $script:RetailDecryptProc = $proc
    $ui.Form.Add_FormClosing({
        param($sender, $e)
        if ($script:RetailDecryptProc -and -not $script:RetailDecryptProc.HasExited) {
            $script:RetailDecryptCancelled = $true
            try { $script:RetailDecryptProc.Kill() } catch { }
        }
    })

    while (-not $proc.HasExited) {
        [System.Windows.Forms.Application]::DoEvents()
        Start-Sleep -Milliseconds 200
        if ($ui.Form.IsDisposed) { break }
    }

    $ui.Timer.Stop()
    if (-not $ui.Form.IsDisposed) { $ui.Form.Close() }

    $stdout = ''
    if (Test-Path -LiteralPath $stdoutFile) {
        $stdout = Get-Content -LiteralPath $stdoutFile -Raw -Encoding UTF8
        Remove-Item -LiteralPath $stdoutFile -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $progressFile -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $stderrFile -Force -ErrorAction SilentlyContinue

    if ($script:RetailDecryptCancelled) {
        Write-Log 'Retail decrypt cancelled by user'
        if ($ephemeral) { Remove-EphemeralCacheDirs -CleanupDirs @($sessionDir) -SkipBackup }
        return $PromptedVolumes
    }

    $exitCode = $proc.ExitCode
    $result = @{ ExitCode = $exitCode; StdOut = $stdout }

    if ($exitCode -ne 0) {
        $msg = 'Decryption failed.'
        if ($stdout) {
            $lines = $stdout -split "`n" | Where-Object { $_ -match '"type"\s*:\s*"error"' }
            if ($lines) {
                try {
                    $err = $lines[-1] | ConvertFrom-Json
                    $msg = $err.message
                }
                catch { }
            }
        }
        Write-Log "Retail decrypt failed (exit $exitCode): $msg"
        if ($ephemeral) { Remove-EphemeralCacheDirs -CleanupDirs @($sessionDir) -SkipBackup }
        [void][System.Windows.Forms.MessageBox]::Show(
            "$msg`n`nSee disc-run.log. Ensure your drive is on the RPCS3 compatible list and the game has a known IRD key.",
            'PES3-Disc',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        )
        return $PromptedVolumes
    }

    $dumpResult = $null
    foreach ($line in ($stdout -split "`n")) {
        if ($line -match '"Success"\s*:\s*true') {
            try { $dumpResult = $line | ConvertFrom-Json } catch { }
        }
    }

    if (-not $dumpResult -or -not $dumpResult.Eboot) {
        if ($ephemeral) {
            Remove-EphemeralCacheDirs -CleanupDirs @($sessionDir) -SkipBackup
        }
        $cached = Get-CachedRetailDump -VolumeId $Drive.Id -ProductCode $null
        if (-not $cached) {
            Write-Log 'Decrypt finished but EBOOT not located'
            return $PromptedVolumes
        }
        $game = $cached.Game
    }
    else {
        $gameRoot = $dumpResult.GameRoot
        $ebootPath = $dumpResult.Eboot

        if (-not $ephemeral -and $dumpResult.ProductCode -and $gameRoot) {
            $cacheRoot = Get-DumpCacheRoot
            $finalCache = Join-Path $cacheRoot $dumpResult.ProductCode
            if (Test-Path -LiteralPath $gameRoot) {
                if (Test-Path -LiteralPath $finalCache) {
                    $oldEboot = Join-Path $finalCache 'PS3_GAME\USRDIR\EBOOT.BIN'
                    if ((Test-BackupsEnabled) -and (Test-Path -LiteralPath $oldEboot)) {
                        Invoke-Pes3Backup -EbootPath $oldEboot -SourceDirs @($finalCache) -Reason 'before_cache_replace' | Out-Null
                    }
                    Remove-Item -LiteralPath $finalCache -Recurse -Force -ErrorAction SilentlyContinue
                }
                Move-Item -LiteralPath $gameRoot -Destination $finalCache -ErrorAction SilentlyContinue
                $gameRoot = $finalCache
                $ebootPath = Join-Path $finalCache 'PS3_GAME\USRDIR\EBOOT.BIN'
            }
        }

        $cleanupDirs = @()
        if ($ephemeral) {
            if ($gameRoot) { $cleanupDirs += $gameRoot }
            $cleanupDirs += $sessionDir
        }

        $game = @{
            Eboot                = $ebootPath
            Title                = $dumpResult.Title
            EphemeralCleanupDirs = $cleanupDirs
        }
    }

    Write-Log "Retail decrypt OK: $($game.Eboot)"
    return Invoke-DiscPrompt -Drive $Drive -Game $game -PromptedVolumes $PromptedVolumes
}

function Test-RetailDecryptEnabled {
    $config = Get-Config
    if ($config -and $null -ne $config.EnableRetailDecrypt) {
        return [bool]$config.EnableRetailDecrypt
    }
    return $true
}

function Test-DecryptUnknownDisc {
    $config = Get-Config
    if ($config -and $null -ne $config.DecryptUnknownOpticalMedia) {
        return [bool]$config.DecryptUnknownOpticalMedia
    }
    return $false
}

function Update-DiscScan {
    param(
        [int]$DelaySeconds = 3,
        [switch]$RemoveOnly,
        [string[]]$TestVolumeRoots = @(),
        [switch]$NonInteractive,
        [switch]$ClearTestVolumes
    )

    $Script:NonInteractive = [bool]$NonInteractive
    $prompted = Get-PromptedVolumes

    if ($RemoveOnly) {
        if ($ClearTestVolumes) {
            foreach ($key in @($prompted.Keys)) {
                if ($key.StartsWith('TESTVOL|')) {
                    $prompted.Remove($key) | Out-Null
                    Write-Log "Volume removed, reset prompt state: $key"
                }
            }
        }

        $present = @{}
        $volumeList = if ($TestVolumeRoots.Count -gt 0) {
            Get-TestVolumeDrives -Roots $TestVolumeRoots
        }
        else {
            Get-OpticalDrives
        }
        foreach ($d in $volumeList) { $present[$d.Id] = $true }
        foreach ($key in @($prompted.Keys)) {
            if (-not $present.ContainsKey($key)) {
                $prompted.Remove($key) | Out-Null
                Write-Log "Volume removed, reset prompt state: $key"
            }
        }
        Set-PromptedVolumes -Volumes $prompted
        return
    }

    [void](Initialize-Pes3DataPaths)

    if ($DelaySeconds -gt 0) {
        Start-Sleep -Seconds $DelaySeconds
    }

    if ($TestVolumeRoots.Count -gt 0) {
        $driveList = @(Get-TestVolumeDrives -Roots $TestVolumeRoots)
    }
    elseif ($Script:NonInteractive -and $env:GITHUB_ACTIONS -eq 'true') {
        Write-Log 'CI skip: no test volumes and non-interactive scan'
        return
    }
    else {
        $driveList = @(Get-OpticalDrives)
    }

    Write-Log "Scan: $($driveList.Count) volume(s) (testRoots=$($TestVolumeRoots.Count), nonInteractive=$($Script:NonInteractive))"

    foreach ($drive in $driveList) {
        $status = Get-Ps3DiscVolumeStatus -DriveRoot $drive.Root
        switch ($status.Kind) {
            'Playable' {
                if (-not $prompted.ContainsKey($drive.Id)) {
                    Write-Log "Found PS3 game on $($drive.Letter): $($status.Game.Eboot)"
                }
                $prompted = Invoke-DiscPrompt -Drive $drive -Game $status.Game -PromptedVolumes $prompted
            }
            'EncryptedRetail' {
                if (-not $prompted.ContainsKey($drive.Id)) {
                    Write-Log "Drive $($drive.Letter): $($status.Message)"
                }
                if (Test-RetailDecryptEnabled) {
                    $prompted = Invoke-RetailDiscDecrypt -Drive $drive -PromptedVolumes $prompted
                }
            }
            'IncompleteBurn' {
                if (-not $prompted.ContainsKey($drive.Id)) {
                    Write-Log "Drive $($drive.Letter): $($status.Message)"
                }
                if (Test-RetailDecryptEnabled) {
                    $prompted = Invoke-RetailDiscDecrypt -Drive $drive -PromptedVolumes $prompted
                }
            }
            'NoPs3Layout' {
                Write-Log "Drive $($drive.Letter): $($status.Message)"
                if ((Test-RetailDecryptEnabled) -and (Test-DecryptUnknownDisc)) {
                    $prompted = Invoke-RetailDiscDecrypt -Drive $drive -PromptedVolumes $prompted
                }
            }
        }
    }

    $previous = Get-PromptedVolumes
    $changed = ($previous.Keys.Count -ne $prompted.Keys.Count)
    if (-not $changed) {
        foreach ($key in $prompted.Keys) {
            if (-not $previous.ContainsKey($key)) { $changed = $true; break }
        }
    }
    if ($changed) {
        Set-PromptedVolumes -Volumes $prompted
    }
}

