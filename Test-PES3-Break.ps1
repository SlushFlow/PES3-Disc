# Adversarial break tests for PES3-Disc (DIY + retail fixtures).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fixtures = Join-Path $root 'test-fixtures'
$diyRoot = Join-Path $fixtures 'diy-demo-disc'
$retailRoot = Join-Path $fixtures 'retail-encrypted-disc'
$failures = 0
$passed = 0

function Assert-True {
    param([string]$Name, [bool]$Condition, [string]$Detail = '')
    if ($Condition) {
        Write-Host "PASS: $Name" -ForegroundColor Green
        $script:passed++
    }
    else {
        Write-Host "FAIL: $Name $(if ($Detail) { "- $Detail" })" -ForegroundColor Red
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

. (Join-Path $root 'Ps3DiscRun.ps1')

Write-Host '======== Build fixtures ========' -ForegroundColor Cyan
& (Join-Path $fixtures 'Build-TestFixtures.ps1')

Write-Host '======== Fixture sanity ========' -ForegroundColor Cyan
$diyStatus = Get-Ps3DiscVolumeStatus -DriveRoot $diyRoot
$retailStatus = Get-Ps3DiscVolumeStatus -DriveRoot $retailRoot
Assert-True 'DIY fixture Playable' ($diyStatus.Kind -eq 'Playable') $diyStatus.Kind
Assert-True 'Retail fixture EncryptedRetail' ($retailStatus.Kind -eq 'EncryptedRetail') $retailStatus.Kind

Write-Host '======== Swapped EBOOT headers ========' -ForegroundColor Cyan
$swapRoot = Join-Path $env:TEMP "pes3-break-swap-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory -Path $swapRoot -Force | Out-Null
Copy-Item -LiteralPath $diyRoot -Destination (Join-Path $swapRoot 'diy') -Recurse -Force
Copy-Item -LiteralPath $retailRoot -Destination (Join-Path $swapRoot 'retail') -Recurse -Force
$diyE = Join-Path $swapRoot 'diy\PS3_GAME\USRDIR\EBOOT.BIN'
$retE = Join-Path $swapRoot 'retail\PS3_GAME\USRDIR\EBOOT.BIN'
$diyBytes = [IO.File]::ReadAllBytes($diyE)
$retBytes = [IO.File]::ReadAllBytes($retE)
[IO.File]::WriteAllBytes($diyE, $retBytes)
[IO.File]::WriteAllBytes($retE, $diyBytes)
$swapDiy = Get-Ps3DiscVolumeStatus -DriveRoot (Join-Path $swapRoot 'diy')
$swapRet = Get-Ps3DiscVolumeStatus -DriveRoot (Join-Path $swapRoot 'retail')
Assert-True 'Swapped DIY -> EncryptedRetail' ($swapDiy.Kind -eq 'EncryptedRetail') $swapDiy.Kind
Assert-True 'Swapped retail -> Playable' ($swapRet.Kind -eq 'Playable') $swapRet.Kind
Remove-Item -LiteralPath $swapRoot -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '======== Incomplete burn with PARAM.SFO ========' -ForegroundColor Cyan
$incomplete = Join-Path $env:TEMP "pes3-break-incomplete-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory -Path (Join-Path $incomplete 'PS3_GAME\USRDIR') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $diyRoot 'PS3_GAME\PARAM.SFO') -Destination (Join-Path $incomplete 'PS3_GAME\PARAM.SFO')
$incStatus = Get-Ps3DiscVolumeStatus -DriveRoot $incomplete
Assert-True 'PARAM without EBOOT is IncompleteBurn' ($incStatus.Kind -eq 'IncompleteBurn') $incStatus.Kind
Remove-Item -LiteralPath $incomplete -Recurse -Force -ErrorAction SilentlyContinue

Write-Host '======== Parallel scan lock stress ========' -ForegroundColor Cyan
$discRun = Join-Path $root 'DiscRun.ps1'
$p1 = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$discRun`"",
    '-Scan', '-NonInteractive', '-TestVolume', "`"$diyRoot`""
) -PassThru -WindowStyle Hidden
$p2 = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$discRun`"",
    '-Scan', '-NonInteractive', '-TestVolume', "`"$retailRoot`""
) -PassThru -WindowStyle Hidden
Wait-ProcessWithTimeout -ProcessIds @($p1.Id, $p2.Id) -TimeoutSeconds 120
$p1.Refresh()
$p2.Refresh()
Assert-True 'Parallel DiscRun scans exit 0' (($p1.ExitCode -eq 0) -and ($p2.ExitCode -eq 0)) "codes $($p1.ExitCode), $($p2.ExitCode)"

Write-Host '======== Empty / garbage roots ========' -ForegroundColor Cyan
$garbage = Join-Path $env:TEMP "pes3-break-garbage-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory -Path $garbage -Force | Out-Null
Set-Content -LiteralPath (Join-Path $garbage 'random.txt') -Value 'not a ps3 disc'
$garbStatus = Get-Ps3DiscVolumeStatus -DriveRoot $garbage
Assert-True 'Garbage folder NoPs3Layout' ($garbStatus.Kind -eq 'NoPs3Layout') $garbStatus.Kind
Remove-Item -LiteralPath $garbage -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
Write-Host "Break test results: $passed passed, $failures failed" -ForegroundColor $(if ($failures -eq 0) { 'Green' } else { 'Red' })
if ($failures -gt 0) { exit 1 }
exit 0
