# Builds dist\ then compiles PES3-Disc-Setup.exe (Inno Setup).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$installerDir = Join-Path $root 'installer'
$iss = Join-Path $installerDir 'PES3-Disc.iss'
$outDir = Join-Path $installerDir 'output'

Write-Host 'Step 1/2: Building PES3-Disc application...'
& (Join-Path $root 'Build-App.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build-App.ps1 failed.' }

if (-not (Test-Path -LiteralPath (Join-Path $root 'dist\PES3-Disc.exe'))) {
    throw 'dist\PES3-Disc.exe not found after build.'
}

Write-Host ''
Write-Host 'Step 2/2: Compiling installer...'

$iscc = @(
    (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1

if (-not $iscc) {
    if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true') {
        throw 'Inno Setup 6 not found on build agent.'
    }
    Write-Host ''
    Write-Host 'Inno Setup 6 not found.' -ForegroundColor Yellow
    Write-Host 'Install from: https://jrsoftware.org/isdl.php' -ForegroundColor Yellow
    Write-Host ''
    Write-Host 'Or run the PowerShell installer (Admin):' -ForegroundColor Cyan
    Write-Host "  powershell -ExecutionPolicy Bypass -File installer\Install-PES3-Disc.ps1"
    Write-Host ''
    Write-Host 'App binaries are ready in dist\'
    exit 0
}

$distDir = Join-Path $root 'dist'
if (-not (Test-Path -LiteralPath (Join-Path $distDir 'PES3-Disc.exe'))) {
    throw "dist\PES3-Disc.exe missing before Inno compile."
}

$appVersion = '1.0.0'
if ($env:PES3_VERSION -match '^(\d+\.\d+\.\d+)$') {
    $appVersion = $Matches[1]
}

if (Test-Path -LiteralPath $outDir) {
    Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$logPath = Join-Path $outDir 'iscc.log'
$isccArgs = @(
    "/DMyAppVersion=$appVersion",
    "/O$outDir",
    "/Log=$logPath",
    (Split-Path -Leaf $iss)
)
if (-not $env:GITHUB_ACTIONS) {
    $isccArgs = @('/Qp') + $isccArgs
}

Write-Host 'dist contents:'
Get-ChildItem -LiteralPath $distDir | ForEach-Object { Write-Host "  $($_.Name)" }
Write-Host "ISCC: $iscc $($isccArgs -join ' ')"
Push-Location $installerDir
try {
    $attempt = 0
    do {
        $attempt++
        & $iscc @isccArgs
        if ($LASTEXITCODE -eq 0) { break }
        if ($attempt -lt 3) {
            Write-Host "Inno compile failed (exit $LASTEXITCODE), retry $attempt/3..." -ForegroundColor Yellow
            Start-Sleep -Seconds 3
        }
    } while ($attempt -lt 3)
}
finally {
    Pop-Location
}

if ($LASTEXITCODE -ne 0) {
    if (Test-Path -LiteralPath $logPath) {
        Write-Host '--- ISCC log ---' -ForegroundColor Red
        $logText = Get-Content -LiteralPath $logPath -Raw
        Write-Host $logText
        if ($env:GITHUB_ACTIONS -eq 'true') {
            "ISCC_LOG<<EOF`n$logText`nEOF" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
        }
    }
    throw "Inno Setup compile failed (exit $LASTEXITCODE). See iscc.log above."
}

$setup = Get-ChildItem -LiteralPath $outDir -Filter 'PES3-Disc-Setup*.exe' | Select-Object -First 1
Write-Host ''
Write-Host "Installer created: $($setup.FullName)"
Write-Host ''
Write-Host 'Give users this file. It will:'
Write-Host '  - Download and install .NET 8 Desktop Runtime'
Write-Host '  - Download and install .NET 10 Desktop Runtime'
Write-Host '  - Install PES3-Disc to Program Files'
Write-Host '  - Add Start menu shortcut'
