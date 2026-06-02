# PES3-Disc core library (config, paths, disc detection, retail decrypt).

# Shared helpers for PES3-Disc (PlayStation Emulation Station 3 Disc)

$Script:Root = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$Script:ConfigPath = Join-Path $Script:Root 'config.json'
$Script:PromptedVolumesPath = Join-Path $Script:Root 'prompted-volumes.json'
$Script:LogPath = Join-Path $Script:Root 'disc-run.log'

function Write-Log {
    param([string]$Message)
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $Message
    try { Add-Content -Path $Script:LogPath -Value $line -Encoding UTF8 } catch { }
}

function Get-PromptedVolumes {
    if (-not (Test-Path -LiteralPath $Script:PromptedVolumesPath)) {
        return @{}
    }
    try {
        $list = Get-Content -LiteralPath $Script:PromptedVolumesPath -Raw -Encoding UTF8 | ConvertFrom-Json
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
    ($list | ConvertTo-Json) | Set-Content -LiteralPath $Script:PromptedVolumesPath -Encoding UTF8
}

function Get-Config {
    if (-not (Test-Path -LiteralPath $Script:ConfigPath)) {
        return $null
    }
    try {
        return Get-Content -LiteralPath $Script:ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
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
    foreach ($sub in @('cache', 'logs', 'state', 'temp')) {
        $p = Join-Path $pes3 $sub
        if (-not (Test-Path -LiteralPath $p)) {
            New-Item -ItemType Directory -Path $p -Force | Out-Null
        }
    }

    $Script:Pes3Root = $pes3
    $Script:LogPath = Join-Path $pes3 'logs\disc-run.log'
    $Script:PromptedVolumesPath = Join-Path $pes3 'state\prompted-volumes.json'
    $Script:PathsInitialized = $true
    return $true
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

    $pathsFile = Join-Path $env:TEMP ("pes3-cleanup-paths-{0}.json" -f [Guid]::NewGuid().ToString('N'))
    ($dirs | ConvertTo-Json) | Set-Content -LiteralPath $pathsFile -Encoding UTF8

    $pathsFileEscaped = $pathsFile.Replace("'", "''")
    $cleanupCmd = @"
`$ErrorActionPreference = 'SilentlyContinue'
Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue
Start-Sleep -Seconds 4
`$paths = Get-Content -LiteralPath '$pathsFileEscaped' -Raw -Encoding UTF8 | ConvertFrom-Json
foreach (`$d in `$paths) {
    if (`$d -and (Test-Path -LiteralPath `$d)) {
        Remove-Item -LiteralPath `$d -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Remove-Item -LiteralPath '$pathsFileEscaped' -Force -ErrorAction SilentlyContinue
"@

    Write-Log "Scheduled cache cleanup after RPCS3 (PID $ProcessId): $($dirs -join '; ')"
    Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $cleanupCmd
    ) | Out-Null
}

function Remove-EphemeralCacheDirs {
    param([string[]]$CleanupDirs)

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
    }
    ($obj | ConvertTo-Json) | Set-Content -LiteralPath $Script:ConfigPath -Encoding UTF8
    $Script:PathsInitialized = $false
    [void](Initialize-Pes3DataPaths)
}

function Read-ParamSfoTitle {
    param([string]$SfoPath)
    if (-not (Test-Path -LiteralPath $SfoPath)) { return $null }

    try {
        $bytes = [System.IO.File]::ReadAllBytes($SfoPath)
        if ($bytes.Length -lt 20) { return $null }

        # SFO magic is 0x00 'P' 'S' 'F'
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
            $keyName = [System.Text.Encoding]::ASCII.GetString($bytes, $nameOff, $nameEnd - $nameOff)

            if ($keyName -eq 'TITLE') {
                $absOff = $dataTableOff + $dataOff
                if ($absOff + $dataLen -gt $bytes.Length) { return $null }
                $raw = $bytes[$absOff..($absOff + $dataLen - 1)]
                $text = [System.Text.Encoding]::UTF8.GetString($raw).Trim([char]0)
                if ($text) { return $text }
            }
        }
    }
    catch {
        Write-Log "PARAM.SFO read failed ($SfoPath): $_"
    }
    return $null
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
    $ps3Game = Split-Path (Split-Path $EbootPath -Parent) -Parent
    $sfo = Join-Path $ps3Game 'PARAM.SFO'
    $title = Read-ParamSfoTitle -SfoPath $sfo
    return @{
        Eboot      = $EbootPath
        Title      = $title
        Ps3GameDir = $ps3Game
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
        foreach ($dir in Get-ChildItem -LiteralPath $DriveRoot -Directory -ErrorAction SilentlyContinue) {
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
        $bytes = [IO.File]::ReadAllBytes($Path)
        if ($bytes.Length -lt 7) { return $false }
        return ($bytes[0] -eq 0x53 -and $bytes[1] -eq 0x43 -and $bytes[2] -eq 0x45 -and $bytes[6] -eq 2)
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

    if ($hasParam -or $hasSfb) {
        return @{
            Kind    = 'EncryptedRetail'
            Message = 'Retail PS3 disc structure detected (encrypted or partial mount).'
            Game    = $null
        }
    }

    if ($hasSfb) {
        return @{
            Kind    = 'IncompleteBurn'
            Message = 'PS3_DISC.SFB found but PS3_GAME\USRDIR\EBOOT.BIN is missing.'
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
    $drives = @()
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
    return $drives
}

function Start-Rpcs3Game {
    param(
        [string]$Rpcs3Path,
        [string]$EbootPath,
        [bool]$UseNoGui,
        [string[]]$EphemeralCleanupDirs = @()
    )

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

    Add-Type -AssemblyName System.Windows.Forms

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

    $cleanupDirs = Get-PathsToCleanupForGame -Game $Game

    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
        $useNoGui = $false
        if ($config -and $null -ne $config.UseNoGui) {
            $useNoGui = [bool]$config.UseNoGui
        }
        Start-Rpcs3Game -Rpcs3Path $rpcs3 -EbootPath $Game.Eboot -UseNoGui $useNoGui -EphemeralCleanupDirs $cleanupDirs
    }
    else {
        Write-Log "User declined: $($Game.Eboot)"
        if ($cleanupDirs.Count -gt 0) {
            Remove-EphemeralCacheDirs -CleanupDirs $cleanupDirs
        }
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

function Get-CachedRetailDump {
    param(
        [string]$VolumeId,
        [string]$ProductCode
    )

    if (Test-DeleteCacheAfterPlay) {
        return $null
    }

    $cacheRoot = Get-DumpCacheRoot
    $search = @()
    if ($ProductCode) {
        $search += Join-Path $cacheRoot $ProductCode
    }
    if ($VolumeId) {
        $safe = ($VolumeId -replace '[\\/:*?"<>|]', '_').Substring(0, [Math]::Min(64, ($VolumeId -replace '[\\/:*?"<>|]', '_').Length))
        $search += Join-Path $cacheRoot $safe
    }

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

function Show-DecryptProgressForm {
    param(
        [string]$Title,
        [scriptblock]$OnCancel
    )

    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

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

    $dumpCli = Get-DumpCliPath
    if (-not $dumpCli) {
        [void][System.Windows.Forms.MessageBox]::Show(
            @"
Retail disc decryption is not set up yet.

Run Setup.ps1 -RetailDecrypt once to build the decryptor, or set DumpCliPath in config.json.

You need:
â€¢ .NET 10 SDK (to build) or a pre-built pes3-disc-dump.exe in tools\
â€¢ A compatible Blu-ray drive (see RPCS3 quickstart)
â€¢ Enough free disk space for the full game
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

    Add-Type -AssemblyName System.Windows.Forms
    $confirm = [System.Windows.Forms.MessageBox]::Show(
        @"
A retail PS3 disc was detected on drive $($Drive.Letter):.

PES3-Disc can decrypt it and launch RPCS3. This requires:
â€¢ A compatible Blu-ray drive (MediaTek chipset - see RPCS3 quickstart)
â€¢ PS3_DISC.SFB visible on the disc in Windows (many official discs show at least this)
â€¢ An IRD key in the online databases (same as PS3 Disc Dumper)
â€¢ Tens of GB free space and a long wait (30-90+ min)$pes3Note$ephemeralNote

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
    $argList = @(
        '--output', "`"$sessionDir`"",
        '--drive', $Drive.Letter,
        '--progress', "`"$progressFile`""
    )

    $proc = Start-Process -FilePath $dumpCli `
        -ArgumentList $argList `
        -RedirectStandardOutput $stdoutFile `
        -RedirectStandardError (Join-Path $env:TEMP 'pes3-disc-stderr.txt') `
        -NoNewWindow -PassThru

    [System.Windows.Forms.Application]::EnableVisualStyles()
    $ui.Form.Show()
    $ui.Timer.Start()
    $cancelRequested = $false
    $ui.Form.Add_FormClosing({
        param($sender, $e)
        if ($proc -and -not $proc.HasExited) {
            $cancelRequested = $true
            try { $proc.Kill() } catch { }
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

    if ($cancelRequested) {
        Write-Log 'Retail decrypt cancelled by user'
        if ($ephemeral) { Remove-EphemeralCacheDirs -CleanupDirs @($sessionDir) }
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
        if ($ephemeral) { Remove-EphemeralCacheDirs -CleanupDirs @($sessionDir) }
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
            Remove-EphemeralCacheDirs -CleanupDirs @($sessionDir)
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
        [switch]$RemoveOnly
    )

    $prompted = Get-PromptedVolumes

    if ($RemoveOnly) {
        $present = @{}
        foreach ($d in Get-OpticalDrives) { $present[$d.Id] = $true }
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

    foreach ($drive in Get-OpticalDrives) {
        $status = Get-Ps3DiscVolumeStatus -DriveRoot $drive.Root
        switch ($status.Kind) {
            'Playable' {
                Write-Log "Found PS3 game on $($drive.Letter): $($status.Game.Eboot)"
                $prompted = Invoke-DiscPrompt -Drive $drive -Game $status.Game -PromptedVolumes $prompted
            }
            'EncryptedRetail' {
                Write-Log "Drive $($drive.Letter): $($status.Message)"
                if (Test-RetailDecryptEnabled) {
                    $prompted = Invoke-RetailDiscDecrypt -Drive $drive -PromptedVolumes $prompted
                }
            }
            'IncompleteBurn' {
                Write-Log "Drive $($drive.Letter): $($status.Message)"
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

    Set-PromptedVolumes -Volumes $prompted
}

