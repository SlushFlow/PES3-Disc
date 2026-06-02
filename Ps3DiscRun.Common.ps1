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

function Save-Config {
    param(
        [string]$Rpcs3Path,
        [int]$ScanDelaySeconds = 3,
        [bool]$UseNoGui = $false
    )
    $obj = [ordered]@{
        Rpcs3Path        = $Rpcs3Path
        ScanDelaySeconds = $ScanDelaySeconds
        UseNoGui         = $UseNoGui
    }
    ($obj | ConvertTo-Json) | Set-Content -LiteralPath $Script:ConfigPath -Encoding UTF8
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
        [bool]$UseNoGui
    )

    $argList = @()
    if ($UseNoGui) { $argList += '--no-gui' }
    $argList += $EbootPath

    Write-Log "Launching: $Rpcs3Path $($argList -join ' ')"
    Start-Process -FilePath $Rpcs3Path -ArgumentList $argList -WorkingDirectory (Split-Path $Rpcs3Path -Parent)
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

    if ($result -eq [System.Windows.Forms.DialogResult]::Yes) {
        $useNoGui = $false
        if ($config -and $null -ne $config.UseNoGui) {
            $useNoGui = [bool]$config.UseNoGui
        }
        Start-Rpcs3Game -Rpcs3Path $rpcs3 -EbootPath $Game.Eboot -UseNoGui $useNoGui
    }
    else {
        Write-Log "User declined: $($Game.Eboot)"
    }

    return $PromptedVolumes
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

. (Join-Path $Script:Root 'Ps3DiscRun.Retail.ps1')
