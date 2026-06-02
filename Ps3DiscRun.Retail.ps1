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

Run Setup-RetailDecrypt.ps1 once to build the decryptor, or set DumpCliPath in config.json.

You need:
• .NET 10 SDK (to build) or a pre-built pes3-disc-dump.exe in tools\
• A compatible Blu-ray drive (see RPCS3 quickstart)
• Enough free disk space for the full game
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
• A compatible Blu-ray drive (MediaTek chipset - see RPCS3 quickstart)
• PS3_DISC.SFB visible on the disc in Windows (many official discs show at least this)
• An IRD key in the online databases (same as PS3 Disc Dumper)
• Tens of GB free space and a long wait (30-90+ min)$pes3Note$ephemeralNote

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
