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

function Invoke-Iscc {
    param(
        [string]$Compiler,
        [string]$WorkingDir,
        [string[]]$ArgumentList,
        [string]$StdoutLog,
        [string]$StderrLog
    )
    foreach ($path in @($StdoutLog, $StderrLog)) {
        if ($path -and (Test-Path -LiteralPath $path)) {
            Remove-Item -LiteralPath $path -Force
        }
    }
    $proc = Start-Process -FilePath $Compiler `
        -ArgumentList $ArgumentList `
        -WorkingDirectory $WorkingDir `
        -Wait -PassThru -NoNewWindow `
        -RedirectStandardOutput $StdoutLog `
        -RedirectStandardError $StderrLog
    $stdout = if ($StdoutLog -and (Test-Path -LiteralPath $StdoutLog)) { Get-Content -LiteralPath $StdoutLog -Raw } else { '' }
    $stderr = if ($StderrLog -and (Test-Path -LiteralPath $StderrLog)) { Get-Content -LiteralPath $StderrLog -Raw } else { '' }
    return @{ ExitCode = $proc.ExitCode; Stdout = $stdout; Stderr = $stderr }
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
    Write-Host 'Inno Setup 6 not found.' -ForegroundColor Yellow
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
foreach ($helper in @('Install-DotNet-Runtimes.ps1', 'Install-DotNet-Runtimes.cmd')) {
    Copy-Item -LiteralPath (Join-Path $installerDir $helper) -Destination $stageDir -Force
}

$stagedFiles = @(Get-ChildItem -LiteralPath $stageDir -Recurse -File)
if (-not (Test-Path -LiteralPath (Join-Path $stageDir 'PES3-Disc.exe'))) {
    throw 'installer\stage\PES3-Disc.exe missing after staging.'
}
Write-Host "Staged $($stagedFiles.Count) file(s):"
$stagedFiles | ForEach-Object { Write-Host "  $($_.FullName.Substring($stageDir.Length).TrimStart('\'))" }

if (Test-Path -LiteralPath $outDir) {
    Remove-Item -LiteralPath $outDir -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$stdoutLog = Join-Path $outDir 'iscc-stdout.txt'
$stderrLog = Join-Path $outDir 'iscc-stderr.txt'
$issName = Split-Path -Leaf $iss
$isccArgs = @(
    "/DMyAppVersion=$appVersion",
    $issName
)

Write-Host "ISCC: $iscc"
Write-Host "Args: $($isccArgs -join ' ')"

$exitCode = 1
$stdoutText = ''
$stderrText = ''
Push-Location $installerDir
try {
    for ($attempt = 1; $attempt -le 3; $attempt++) {
        $result = Invoke-Iscc -Compiler $iscc -WorkingDir $installerDir -ArgumentList $isccArgs -StdoutLog $stdoutLog -StderrLog $stderrLog
        $exitCode = $result.ExitCode
        $stdoutText = $result.Stdout
        $stderrText = $result.Stderr
        if ($stdoutText) { Write-Host $stdoutText }
        if ($stderrText) { Write-Host $stderrText }
        if ($exitCode -eq 0) { break }
        if ($attempt -lt 3) {
            Write-Host "Inno compile failed (exit $exitCode), retry $attempt/3..." -ForegroundColor Yellow
            Start-Sleep -Seconds 3
        }
    }
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    Write-Host '--- ISCC stdout ---' -ForegroundColor Red
    Write-Host $(if ($stdoutText) { $stdoutText } else { '(empty)' })
    Write-Host '--- ISCC stderr ---' -ForegroundColor Red
    Write-Host $(if ($stderrText) { $stderrText } else { '(empty)' })

    if ($env:GITHUB_ACTIONS -eq 'true') {
        $summary = @"
### stdout
$($stdoutText.Trim())

### stderr
$($stderrText.Trim())
"@
        "## Inno Setup failed (exit $exitCode)`n`n$summary" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }
    throw "Inno Setup compile failed (exit $exitCode)."
}

$setup = Get-ChildItem -LiteralPath $outDir -Filter 'PES3-Disc-Setup*.exe' | Select-Object -First 1
if (-not $setup) {
    throw "No PES3-Disc-Setup*.exe in $outDir"
}

Write-Host ''
Write-Host "Installer created: $($setup.FullName)"
exit 0
