# Builds pes3-disc-dump.exe (retail disc decryptor) from ps3-disc-dumper sources.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$submodule = Join-Path $root 'external\ps3-disc-dumper'
$git = "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe"
if (-not (Test-Path -LiteralPath $git)) {
    $git = 'git'
}

Write-Host 'PES3-Disc — Setup retail disc decryption'
Write-Host ''

if (-not (Test-Path -LiteralPath $submodule)) {
    Write-Host 'Downloading ps3-disc-dumper source...'
    New-Item -ItemType Directory -Path (Split-Path $submodule -Parent) -Force | Out-Null
    & $git clone --depth 1 https://github.com/13xforever/ps3-disc-dumper.git $submodule
    if ($LASTEXITCODE -ne 0) {
        Write-Host 'Clone failed. Ensure Git is installed and you have network access.'
        exit 1
    }
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Write-Host 'ERROR: .NET SDK not found. Install .NET 10 SDK from https://dotnet.microsoft.com/download'
    exit 1
}

$outDir = Join-Path $root 'tools'
$proj = Join-Path $root 'tools\PES3-Disc.DumpCli\PES3-Disc.DumpCli.csproj'

Write-Host 'Publishing pes3-disc-dump.exe…'
& dotnet publish $proj -c Release -r win-x64 --self-contained false -o $outDir
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Build failed.'
    exit 1
}

$exe = Join-Path $outDir 'pes3-disc-dump.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host "Expected $exe was not created."
    exit 1
}

Write-Host ''
Write-Host 'Success:'
Write-Host "  $exe"
Write-Host ''
Write-Host 'Retail decrypt is ready. Start PES3-Disc and insert an official PS3 disc.'
Write-Host 'Requires a compatible Blu-ray drive (see RPCS3 quickstart).'
