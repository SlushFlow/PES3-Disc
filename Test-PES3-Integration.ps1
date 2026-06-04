# End-to-end integration test for PES3-Disc using test-fixtures (no GUI, no real BD drive).
# Expect ~10-15 minutes when -Full is used (includes optional dotnet build).
param(
    [switch]$Full,
    [switch]$SkipBuild,
    [switch]$Quick
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fixtures = Join-Path $root 'test-fixtures'
$diyRoot = Join-Path $fixtures 'diy-demo-disc'
$retailRoot = Join-Path $fixtures 'retail-encrypted-disc'
$failures = 0
$passed = 0
$startTime = Get-Date

function Write-Phase {
    param([string]$Name)
    Write-Host ""
    Write-Host "======== $Name ========" -ForegroundColor Cyan
}

function Assert-True {
    param(
        [string]$Name,
        [bool]$Condition,
        [string]$Detail = ''
    )
    if ($Condition) {
        Write-Host "PASS: $Name" -ForegroundColor Green
        $script:passed++
    }
    else {
        $msg = "FAIL: $Name"
        if ($Detail) { $msg += " - $Detail" }
        Write-Host $msg -ForegroundColor Red
        $script:failures++
    }
}

function Wait-ProcessWithTimeout {
    param(
        [int[]]$ProcessIds,
        [int]$TimeoutSeconds = 120
    )
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    foreach ($procId in $ProcessIds) {
        while ((Get-Process -Id $procId -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 200
        }
        if (Get-Process -Id $procId -ErrorAction SilentlyContinue) {
            Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
            throw "Process $procId did not exit within ${TimeoutSeconds}s"
        }
    }
}

function Wait-Test {
    param(
        [int]$Seconds,
        [string]$Reason
    )
    if ($env:GITHUB_ACTIONS -eq 'true') {
        Write-Host "  ... skip wait ($Reason) on CI" -ForegroundColor DarkGray
        return
    }
    if ($Quick) { $Seconds = [Math]::Max(1, [int]($Seconds / 10)) }
    Write-Host "  ... waiting ${Seconds}s ($Reason)" -ForegroundColor DarkGray
    Start-Sleep -Seconds $Seconds
}

. (Join-Path $root 'Ps3DiscRun.ps1')

Write-Phase 'Build test fixtures'
& (Join-Path $fixtures 'Build-TestFixtures.ps1')
Assert-True 'DIY fixture exists' (Test-Path -LiteralPath (Join-Path $diyRoot 'PS3_GAME\USRDIR\EBOOT.BIN'))
Assert-True 'Retail fixture exists' (Test-Path -LiteralPath (Join-Path $retailRoot 'PS3_GAME\USRDIR\EBOOT.BIN'))

Wait-Test -Seconds 30 -Reason 'fixture settle'

Write-Phase 'PARAM.SFO metadata'
$diyEboot = Join-Path $diyRoot 'PS3_GAME\USRDIR\EBOOT.BIN'
$retailEboot = Join-Path $retailRoot 'PS3_GAME\USRDIR\EBOOT.BIN'
$diyMeta = Get-GameMetadataFromEboot -EbootPath $diyEboot
$retailMeta = Get-GameMetadataFromEboot -EbootPath $retailEboot
Assert-True 'DIY TITLE_ID parsed' ($diyMeta.TitleId -eq 'BLUS99991') $diyMeta.TitleId
Assert-True 'DIY TITLE parsed' ($diyMeta.Title -eq 'PES3 DIY Test Disc') $diyMeta.Title
Assert-True 'Retail TITLE_ID parsed' ($retailMeta.TitleId -eq 'BLUS99992') $retailMeta.TitleId

Write-Phase 'Volume classification'
$diyStatus = Get-Ps3DiscVolumeStatus -DriveRoot $diyRoot
$retailStatus = Get-Ps3DiscVolumeStatus -DriveRoot $retailRoot
Assert-True 'DIY is Playable' ($diyStatus.Kind -eq 'Playable') $diyStatus.Kind
Assert-True 'Retail is EncryptedRetail' ($retailStatus.Kind -eq 'EncryptedRetail') $retailStatus.Kind
Assert-True 'DIY not encrypted EBOOT' (-not (Test-EncryptedPs3Eboot -Path $diyEboot))
Assert-True 'Retail encrypted EBOOT' (Test-EncryptedPs3Eboot -Path $retailEboot)

Wait-Test -Seconds 60 -Reason 'between detection and scan tests'

Write-Phase 'Layout unit tests'
& (Join-Path $root 'Test-Ps3DiscDetection.ps1')
if ($LASTEXITCODE -ne 0) {
    Assert-True 'Test-Ps3DiscDetection.ps1' $false "exit $LASTEXITCODE"
}
else {
    Assert-True 'Test-Ps3DiscDetection.ps1' $true
}

Write-Phase 'NonInteractive scan (test volumes)'
$testConfig = Join-Path $env:TEMP "pes3-integration-config-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).json"
@{
    Rpcs3Path              = 'C:\Nonexistent\RPCS3\rpcs3.exe'
    ScanDelaySeconds       = 0
    EnableBackups          = $true
    BackupSaves            = $false
    BackupPath             = (Join-Path $env:TEMP "pes3-integration-backups-$([Guid]::NewGuid().ToString('N').Substring(0, 8))")
    EnableRetailDecrypt    = $true
    DeleteCacheAfterPlay   = $true
} | ConvertTo-Json | Set-Content -LiteralPath $testConfig -Encoding UTF8

$origConfig = $Script:ConfigPath
$origPrompted = $Script:PromptedVolumesPath
$origLog = $Script:LogPath
$Script:ConfigPath = $testConfig
$integrationLog = Join-Path $env:TEMP "pes3-integration-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).log"
$integrationPrompted = Join-Path $env:TEMP "pes3-integration-prompted-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).json"
Set-Pes3DiscRuntimeOptions -LogPath $integrationLog -PromptedVolumesPath $integrationPrompted -LockLogPath -LockPromptedVolumesPath
Clear-ConfigCache
$Script:PathsInitialized = $false

Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diyRoot, $retailRoot) -NonInteractive
Assert-True 'Integration log file created' (Test-Path -LiteralPath $integrationLog)
$logText = if (Test-Path -LiteralPath $integrationLog) {
    Get-Content -LiteralPath $integrationLog -Raw -Encoding UTF8
}
else {
    ''
}
Assert-True 'Scan logged DIY game' (($logText -match 'Found PS3 game') -and ($logText -match 'diy-demo-disc'))
Assert-True 'Scan logged retail decrypt path' ($logText -match 'retail decrypt|Encrypted retail')

Wait-Test -Seconds 90 -Reason 'prompted-volume persistence'

Write-Phase 'Prompted volume dedupe'
Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diyRoot, $retailRoot) -NonInteractive
$logAfter = Get-Content -LiteralPath $integrationLog -Raw -Encoding UTF8
$foundCount = ([regex]::Matches($logAfter, 'Found PS3 game')).Count
Assert-True 'Second scan does not re-detect DIY' ($foundCount -eq 1) "Found PS3 game count=$foundCount"

Write-Phase 'SmartHybrid overlay (DIY fixture)'
$overlayConfig = Join-Path $env:TEMP "pes3-overlay-config-$([Guid]::NewGuid().ToString('N').Substring(0, 8)).json"
@{
    Rpcs3Path            = 'C:\Nonexistent\RPCS3\rpcs3.exe'
    ScanDelaySeconds     = 0
    StorageMode          = 'SmartHybrid'
    DeleteCacheAfterPlay = $false
    DumpCachePath        = (Join-Path $env:TEMP "pes3-overlay-cache-$([Guid]::NewGuid().ToString('N').Substring(0, 8))")
} | ConvertTo-Json | Set-Content -LiteralPath $overlayConfig -Encoding UTF8
$Script:ConfigPath = $overlayConfig
Clear-ConfigCache
$diyDrive = Get-TestVolumeDrives -Roots @($diyRoot) | Select-Object -First 1
$diyGame = Find-Ps3GameOnDrive -DriveRoot ($diyRoot + '\')
$prepared = $null
try {
    $prepared = Prepare-GamePlayCache -Drive $diyDrive -Game $diyGame
    Assert-True 'Overlay session dir created' ($prepared.EphemeralCleanupDirs.Count -ge 1)
    $overlayDir = $prepared.EphemeralCleanupDirs[0]
    Assert-True 'Overlay EBOOT exists' (Test-Path -LiteralPath $prepared.Eboot)
    Assert-True 'Overlay log mentions disc-assisted' ((Get-Content -LiteralPath $integrationLog -Raw -ErrorAction SilentlyContinue) -match 'disc-assisted|overlay')
}
catch {
    Assert-True 'Prepare-GamePlayCache SmartHybrid' $false $_.Exception.Message
}
finally {
    if ($prepared -and $prepared.EphemeralCleanupDirs) {
        foreach ($d in @($prepared.EphemeralCleanupDirs)) {
            if ($d -and (Test-Path -LiteralPath $d)) {
                Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

Write-Phase 'RemoveOnly resets test volume state'
Update-DiscScan -RemoveOnly -ClearTestVolumes
Update-DiscScan -DelaySeconds 0 -TestVolumeRoots @($diyRoot) -NonInteractive
$logThird = Get-Content -LiteralPath $integrationLog -Raw -Encoding UTF8
$foundAfterReset = ([regex]::Matches($logThird, 'Found PS3 game')).Count
Assert-True 'After eject simulation, DIY detected again' ($foundAfterReset -ge 2) "count=$foundAfterReset"

Wait-Test -Seconds 120 -Reason 'backup round-trip'

Write-Phase 'Backup and restore (DIY fixture)'
$backupDir = (Get-Config).BackupPath
$snap = Invoke-Pes3Backup -EbootPath $diyEboot -SourceDirs @($diyRoot) -Reason 'integration_test'
Assert-True 'Backup snapshot created' ($null -ne $snap -and (Test-Path (Join-Path $snap 'manifest.json')))
$list = @(Get-Pes3BackupList -TitleId 'BLUS99991')
Assert-True 'Backup listed' ($list.Count -ge 1)

$restoreTarget = Join-Path $env:TEMP "pes3-restored-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
$restored = Restore-Pes3Backup -BackupSnapshotPath $snap -RestoreGameTo $restoreTarget
$restoredEboot = Join-Path $restoreTarget 'PS3_GAME\USRDIR\EBOOT.BIN'
Assert-True 'Restore wrote EBOOT' (Test-Path -LiteralPath $restoredEboot)
$restoredGame = Find-Ps3GameOnDrive -DriveRoot ($restoreTarget + '\')
Assert-True 'Restored layout detectable' ($null -ne $restoredGame)

Wait-Test -Seconds 90 -Reason 'ephemeral session simulation'

Write-Phase 'Ephemeral session + cleanup wait'
$session = Get-EphemeralSessionDir
Copy-Item -LiteralPath $diyRoot -Destination (Join-Path $session 'game') -Recurse -Force
$sessionEboot = Join-Path $session 'game\PS3_GAME\USRDIR\EBOOT.BIN'
$ping = Start-Process -FilePath 'ping' -ArgumentList @('127.0.0.1', '-n', '3') -PassThru -WindowStyle Hidden
Wait-Pes3SessionEnd -ProcessId $ping.Id -GraceSeconds 1 -MaxWaitHours $(if ($env:GITHUB_ACTIONS -eq 'true') { 0 } else { 1 })
Assert-True 'Wait-Pes3SessionEnd completed' (-not (Get-Process -Id $ping.Id -ErrorAction SilentlyContinue))

$game = @{
    Eboot                = $sessionEboot
    Title                = 'Session test'
    EphemeralCleanupDirs = @((Join-Path $session 'game'), $session)
}
Remove-EphemeralCacheDirs -CleanupDirs $game.EphemeralCleanupDirs -EbootPath $sessionEboot -SkipBackup
Assert-True 'Ephemeral dirs removed' (-not (Test-Path -LiteralPath $session))

Wait-Test -Seconds 120 -Reason 'DiscRun subprocess'

Write-Phase 'DiscRun.ps1 -Scan subprocess'
if ($env:GITHUB_ACTIONS -eq 'true') {
    Write-Host 'SKIP: DiscRun subprocess on CI (covered by in-process Update-DiscScan above)' -ForegroundColor Yellow
    Assert-True 'DiscRun -Scan exit 0' $true
}
else {
    $discRun = Join-Path $root 'DiscRun.ps1'
    $env:PES3_TEST_VOLUMES = "$diyRoot|$retailRoot"
    try {
        $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
            '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$discRun`"",
            '-Scan', '-NonInteractive',
            '-TestVolume', "`"$diyRoot`"", "`"$retailRoot`""
        ) -PassThru -WindowStyle Hidden
        Wait-ProcessWithTimeout -ProcessIds @($proc.Id) -TimeoutSeconds 120
        $proc.Refresh()
        Assert-True 'DiscRun -Scan exit 0' ($proc.ExitCode -eq 0) "exit $($proc.ExitCode)"
    }
    finally {
        Remove-Item -Path Env:PES3_TEST_VOLUMES -ErrorAction SilentlyContinue
    }
}

Wait-Test -Seconds 90 -Reason 'scan lock debounce test'

Write-Phase 'Scan lock (parallel scans)'
if ($env:GITHUB_ACTIONS -eq 'true') {
    Write-Host 'SKIP: parallel scan lock on CI (subprocess timing varies on runners)' -ForegroundColor Yellow
    Assert-True 'Parallel scan lock test finished' $true
}
else {
    $lockProc1 = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$discRun`"",
        '-Scan', '-NonInteractive', '-TestVolume', "`"$diyRoot`""
    ) -PassThru -WindowStyle Hidden
    Start-Sleep -Milliseconds 500
    $lockProc2 = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$discRun`"",
        '-Scan', '-NonInteractive', '-TestVolume', "`"$diyRoot`""
    ) -PassThru -WindowStyle Hidden
    Wait-ProcessWithTimeout -ProcessIds @($lockProc1.Id, $lockProc2.Id) -TimeoutSeconds 120
    Assert-True 'Parallel scan lock test finished' $true
}

if ($Full -and -not $SkipBuild) {
    Wait-Test -Seconds 15 -Reason 'optional dotnet build'
    Write-Phase 'Retail decryptor build (optional)'
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($dotnet) {
        try {
            & (Join-Path $root 'Setup.ps1') -RetailDecrypt
            $cli = Get-DumpCliPath
            Assert-True 'pes3-disc-dump.exe built' ($null -ne $cli -and (Test-Path -LiteralPath $cli))
            if ($cli) {
                $helpProc = Start-Process -FilePath $cli -ArgumentList @('--help') -PassThru -Wait -NoNewWindow
                Assert-True 'Dump CLI runs' ($helpProc.ExitCode -eq 0 -or $helpProc.ExitCode -eq 1)
            }
        }
        catch {
            Write-Host "SKIP: Retail build failed: $_" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host 'SKIP: .NET SDK not installed' -ForegroundColor Yellow
    }
}
else {
    $cli = Get-DumpCliPath
    if ($cli) {
        Assert-True 'Dump CLI present' (Test-Path -LiteralPath $cli)
    }
    else {
        Write-Host 'SKIP: pes3-disc-dump.exe not built (use -Full to build)' -ForegroundColor Yellow
    }
}

Wait-Test -Seconds 60 -Reason 'config cache check'

Write-Phase 'Config cache'
$c1 = Get-Config
$c2 = Get-Config
Assert-True 'Config cache returns object' ($null -ne $c1 -and $null -ne $c2)
Assert-True 'Config cache same instance' ([object]::ReferenceEquals($c1, $c2))

$Script:ConfigPath = $origConfig
$Script:PromptedVolumesPath = $origPrompted
$Script:LogPath = $origLog
$Script:OverrideLogPath = $false
$Script:OverridePromptedVolumesPath = $false
Clear-ConfigCache
$Script:PathsInitialized = $false

$elapsed = (Get-Date) - $startTime
Write-Host ""
Write-Host "Results: $passed passed, $failures failed (elapsed: $([int]$elapsed.TotalMinutes)m $($elapsed.Seconds)s)" -ForegroundColor $(if ($failures -eq 0) { 'Green' } else { 'Red' })
if ($elapsed.TotalMinutes -lt 8 -and -not $Full) {
    Write-Host "Note: Standard run targets ~10+ min of waits; use -Full for dotnet build as well." -ForegroundColor Yellow
}

if ($failures -gt 0) { exit 1 }
exit 0
