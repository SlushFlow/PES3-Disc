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

function Find-Ps3GameOnDrive {
    param([string]$DriveRoot)

    $layouts = @(
        (Join-Path $DriveRoot 'PS3_GAME\USRDIR\EBOOT.BIN'),
        (Join-Path $DriveRoot 'dev_bdvd\PS3_GAME\USRDIR\EBOOT.BIN')
    )

    foreach ($eboot in $layouts) {
        if (Test-Path -LiteralPath $eboot) {
            $ps3Game = Split-Path (Split-Path (Split-Path $eboot -Parent) -Parent) -Parent
            $sfo = Join-Path $ps3Game 'PARAM.SFO'
            $title = Read-ParamSfoTitle -SfoPath $sfo
            return @{
                Eboot = $eboot
                Title = $title
            }
        }
    }

    try {
        foreach ($dir in Get-ChildItem -LiteralPath $DriveRoot -Directory -ErrorAction SilentlyContinue) {
            $eboot = Join-Path $dir.FullName 'PS3_GAME\USRDIR\EBOOT.BIN'
            if (Test-Path -LiteralPath $eboot) {
                $ps3Game = Join-Path $dir.FullName 'PS3_GAME'
                $sfo = Join-Path $ps3Game 'PARAM.SFO'
                $title = Read-ParamSfoTitle -SfoPath $sfo
                return @{
                    Eboot = $eboot
                    Title = $title
                }
            }
        }
    }
    catch { }

    return $null
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
        $game = Find-Ps3GameOnDrive -DriveRoot $drive.Root
        if ($game) {
            Write-Log "Found PS3 game on $($drive.Letter): $($game.Eboot)"
            $prompted = Invoke-DiscPrompt -Drive $drive -Game $game -PromptedVolumes $prompted
        }
    }

    Set-PromptedVolumes -Volumes $prompted
}
