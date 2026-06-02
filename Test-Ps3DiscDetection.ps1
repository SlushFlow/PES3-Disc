# Validates PES3-Disc layout detection against simulated disc roots (DIY / dump layouts).
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Ps3DiscRun.Common.ps1')

$failures = 0
$passed = 0
$testRoot = Join-Path $env:TEMP "PES3-Disc-Test-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"

function New-MinimalPs3Game {
    param([string]$Ps3GameDir)
    $usr = Join-Path $Ps3GameDir 'USRDIR'
    New-Item -ItemType Directory -Path $usr -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $usr 'EBOOT.BIN'), [byte[]](0x7F, 0x45, 0x4C, 0x46))
    # Minimal valid-enough PARAM.SFO header (TITLE key not required for detection tests)
    $sfo = Join-Path $Ps3GameDir 'PARAM.SFO'
    [IO.File]::WriteAllBytes($sfo, [byte[]](0, 0x50, 0x53, 0x46, 1, 0, 0, 0, 0, 0, 0, 0, 0x14, 0, 0, 0, 0x14, 0, 0, 0))
}

function Assert-Found {
    param(
        [string]$Name,
        [string]$Root,
        [string]$ExpectedSuffix
    )
    $game = Find-Ps3GameOnDrive -DriveRoot $Root
    if (-not $game) {
        Write-Host "FAIL: $Name - no game detected" -ForegroundColor Red
        $script:failures++
        return
    }
    $normalized = $game.Eboot.Replace('/', '\')
    if ($normalized -notlike "*$ExpectedSuffix") {
        Write-Host "FAIL: $Name - expected *$ExpectedSuffix, got $($game.Eboot)" -ForegroundColor Red
        $script:failures++
        return
    }
    Write-Host "PASS: $Name" -ForegroundColor Green
    $script:passed++
}

function Assert-NotFound {
    param(
        [string]$Name,
        [string]$Root
    )
    $game = Find-Ps3GameOnDrive -DriveRoot $Root
    if ($game) {
        Write-Host "FAIL: $Name - should not detect game, got $($game.Eboot)" -ForegroundColor Red
        $script:failures++
        return
    }
    Write-Host "PASS: $Name (correctly ignored)" -ForegroundColor Green
    $script:passed++
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null

    # DIY / standard burned disc: PS3_GAME + PS3_DISC.SFB at volume root
    $diy = Join-Path $testRoot 'diy-standard'
    New-Item -ItemType Directory -Path $diy -Force | Out-Null
    Set-Content -Path (Join-Path $diy 'PS3_DISC.SFB') -Value 'TEST' -NoNewline
    New-MinimalPs3Game -Ps3GameDir (Join-Path $diy 'PS3_GAME')
    Assert-Found -Name 'DIY standard burn (root PS3_GAME)' -Root $diy -ExpectedSuffix 'PS3_GAME\USRDIR\EBOOT.BIN'

    # Dump folder burned inside a title subfolder
    $nested = Join-Path $testRoot 'diy-nested'
    $gameDir = Join-Path $nested 'Demons Souls'
    New-Item -ItemType Directory -Path $gameDir -Force | Out-Null
    New-MinimalPs3Game -Ps3GameDir (Join-Path $gameDir 'PS3_GAME')
    Assert-Found -Name 'DIY nested game folder' -Root $nested -ExpectedSuffix 'PS3_GAME\USRDIR\EBOOT.BIN'

    # dev_bdvd layout (matches console / some dumps)
    $bdvd = Join-Path $testRoot 'dev-bdvd'
    New-MinimalPs3Game -Ps3GameDir (Join-Path $bdvd 'dev_bdvd\PS3_GAME')
    Assert-Found -Name 'dev_bdvd layout' -Root $bdvd -ExpectedSuffix 'dev_bdvd\PS3_GAME\USRDIR\EBOOT.BIN'

    # Retail-like: empty / no PS3_GAME (PC cannot read encrypted official disc)
    $retail = Join-Path $testRoot 'retail-empty'
    New-Item -ItemType Directory -Path $retail -Force | Out-Null
    Assert-NotFound -Name 'Retail-style empty volume' -Root $retail

    # Retail-like: only BDMV (movie structure, not a game file tree)
    $bdmv = Join-Path $testRoot 'retail-bdmv'
    New-Item -ItemType Directory -Path (Join-Path $bdmv 'BDMV\STREAM') -Force | Out-Null
    Assert-NotFound -Name 'BDMV-only volume' -Root $bdmv

    # Marker without game files (incomplete burn)
    $markerOnly = Join-Path $testRoot 'marker-only'
    New-Item -ItemType Directory -Path $markerOnly -Force | Out-Null
    Set-Content -Path (Join-Path $markerOnly 'PS3_DISC.SFB') -Value 'TEST' -NoNewline
    Assert-NotFound -Name 'PS3_DISC.SFB without PS3_GAME' -Root $markerOnly

    $status = Get-Ps3DiscVolumeStatus -DriveRoot $diy
    if ($status.Kind -ne 'Playable') {
        Write-Host "FAIL: Get-Ps3DiscVolumeStatus DIY should be Playable, got $($status.Kind)" -ForegroundColor Red
        $failures++
    }
    else {
        Write-Host "PASS: Get-Ps3DiscVolumeStatus DIY = Playable" -ForegroundColor Green
        $passed++
    }

    $statusEmpty = Get-Ps3DiscVolumeStatus -DriveRoot $retail
    if ($statusEmpty.Kind -ne 'NoPs3Layout') {
        Write-Host "FAIL: empty volume status should be NoPs3Layout" -ForegroundColor Red
        $failures++
    }
    else {
        Write-Host "PASS: Get-Ps3DiscVolumeStatus empty = NoPs3Layout" -ForegroundColor Green
        $passed++
    }

    $statusMarker = Get-Ps3DiscVolumeStatus -DriveRoot $markerOnly
    if ($statusMarker.Kind -ne 'EncryptedRetail') {
        Write-Host "FAIL: marker-only should be EncryptedRetail, got $($statusMarker.Kind)" -ForegroundColor Red
        $failures++
    }
    else {
        Write-Host "PASS: Get-Ps3DiscVolumeStatus marker-only = EncryptedRetail" -ForegroundColor Green
        $passed++
    }

    $encrypted = Join-Path $testRoot 'encrypted-eboot'
    New-Item -ItemType Directory -Path (Join-Path $encrypted 'PS3_GAME\USRDIR') -Force | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $encrypted 'PS3_GAME\USRDIR\EBOOT.BIN'), [byte[]](0x53, 0x43, 0x45, 0, 0, 0, 2))
    $statusEnc = Get-Ps3DiscVolumeStatus -DriveRoot $encrypted
    if ($statusEnc.Kind -ne 'EncryptedRetail') {
        Write-Host "FAIL: encrypted EBOOT should be EncryptedRetail" -ForegroundColor Red
        $failures++
    }
    else {
        Write-Host "PASS: encrypted EBOOT = EncryptedRetail" -ForegroundColor Green
        $passed++
    }
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "Results: $passed passed, $failures failed"
if ($failures -gt 0) { exit 1 }
exit 0
