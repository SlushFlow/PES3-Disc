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

    $cleanupScript = Join-Path $Script:Root 'PES3-CleanupCache.ps1'
    if (-not (Test-Path -LiteralPath $cleanupScript)) {
        Write-Log "Cleanup script missing: $cleanupScript"
        return
    }

    Write-Log "Scheduled cache cleanup after RPCS3 (PID $ProcessId): $($dirs -join '; ')"
    Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass',
        '-File', "`"$cleanupScript`"",
        '-ProcessId', $ProcessId,
        '-PathsFile', "`"$pathsFile`""
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
