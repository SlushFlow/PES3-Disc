# Builds dist\ then compiles PES3-Disc-Setup.exe (Inno Setup).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$installerDir = Join-Path $root 'installer'
$iss = Join-Path $installerDir 'PES3-Disc.iss'
$outDir = Join-Path $installerDir 'output'
$stageDir = Join-Path $installerDir 'stage'

function Get-InnoSetupCompiler {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) { return $path }
    }
    $cmd = Get-Command iscc -ErrorAction SilentlyContinue
    if ($cmd -and (Test-Path -LiteralPath $cmd.Source)) {
        return $cmd.Source
    }
    return $null
}

Write-Host 'Step 1/2: Building PES3-Disc application...'
& (Join-Path $root 'Build-App.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build-App.ps1 failed.' }

$distDir = Join-Path $root 'dist'
if (-not (Test-Path -LiteralPath (Join-Path $distDir 'PES3-Disc.exe'))) {
    throw 'dist\PES3-Disc.exe not found after build.'
}

Write-Host ''
Write-Host 'Step 2/2: Compiling installer...'

$iscc = Get-InnoSetupCompiler
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

$appVersion = '1.0.0'
if ($env:PES3_VERSION -match '^(\d+\.\d+\.\d+)$') {
    $appVersion = $Matches[1]
}

Write-Host 'Staging installer payload...'
if (Test-Path -LiteralPath $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Get-ChildItem -Path $distDir -Force | Copy-Item -Destination $stageDir -Recurse -Force
Copy-Item -LiteralPath (Join-Path $installerDir 'Install-DotNet-Runtimes.ps1') -Destination $stageDir -Force

if (-not (Test-Path -LiteralPath (Join-Path $stageDir 'PES3-Disc.exe'))) {
    throw 'installer\stage\PES3-Disc.exe missing after staging.'
}
if (-not (Test-Path -LiteralPath (Join-Path $stageDir 'Install-DotNet-Runtimes.ps1'))) {
    throw 'installer\stage\Install-DotNet-Runtimes.ps1 missing after staging.'
}
Write-Host "Staged $((Get-ChildItem -LiteralPath $stageDir -Recurse -File).Count) file(s) in installer\stage\"

if (Test-Path -LiteralPath $outDir) {
    Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$logPath = Join-Path $outDir 'iscc.log'
$issName = Split-Path -Leaf $iss

Write-Host "ISCC: $iscc"
Write-Host "ISS:  $iss"
Write-Host "Log:  $logPath"

Push-Location $installerDir
try {
    $attempt = 0
    $exitCode = 1
    do {
        $attempt++
        $proc = Start-Process -FilePath $iscc -ArgumentList @(
            "/DMyAppVersion=$appVersion",
            '/Qp',
            "/Log=$logPath",
            $issName
        ) -Wait -PassThru -NoNewWindow
        $exitCode = $proc.ExitCode
        if ($exitCode -eq 0) { break }
        if ($attempt -lt 3) {
            Write-Host "Inno compile failed (exit $exitCode), retry $attempt/3..." -ForegroundColor Yellow
            Start-Sleep -Seconds 3
        }
    } while ($attempt -lt 3)
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    $logText = $null
    if (Test-Path -LiteralPath $logPath) {
        $logText = Get-Content -LiteralPath $logPath -Raw
    }
    Write-Host '--- ISCC log ---' -ForegroundColor Red
    if ($logText) { Write-Host $logText }
    if ($env:GITHUB_ACTIONS -eq 'true') {
        $summary = if ($logText) { $logText } else { "No log at $logPath (exit $exitCode)" }
        "## Inno Setup failed (exit $exitCode)`n`n``````n$summary`n``````" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }
    throw "Inno Setup compile failed (exit $exitCode)."
}

$setup = Get-ChildItem -LiteralPath $outDir -Filter 'PES3-Disc-Setup*.exe' | Select-Object -First 1
if (-not $setup) {
    throw "No PES3-Disc-Setup*.exe in $outDir"
}

Write-Host ''
Write-Host "Installer created: $($setup.FullName)"
Write-Host ''
Write-Host 'Give users this file. It will:'
Write-Host '  - Download and install .NET 8 Desktop Runtime'
Write-Host '  - Download and install .NET 10 Desktop Runtime'
Write-Host '  - Install PES3-Disc to Program Files'
Write-Host '  - Add Start menu shortcut'

exit 0
