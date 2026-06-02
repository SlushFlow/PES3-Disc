# Builds PES3-Disc.exe (self-contained GUI) into dist\
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$git = "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe"
if (-not (Test-Path -LiteralPath $git)) { $git = 'git' }

$submodule = Join-Path $root 'external\ps3-disc-dumper'
if (-not (Test-Path -LiteralPath (Join-Path $submodule 'Ps3DiscDumper\Ps3DiscDumper.csproj'))) {
    Write-Host 'Cloning ps3-disc-dumper (required for retail decrypt in the GUI)...'
    New-Item -ItemType Directory -Path (Split-Path $submodule -Parent) -Force | Out-Null
    & $git clone --depth 1 https://github.com/13xforever/ps3-disc-dumper.git $submodule
    if ($LASTEXITCODE -ne 0) { throw 'Failed to clone ps3-disc-dumper.' }
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw '.NET 10 SDK required: https://dotnet.microsoft.com/download'
}

$out = Join-Path $root 'dist'
Write-Host "Publishing PES3-Disc.exe to $out ..."
& dotnet publish (Join-Path $root 'src\PES3-Disc.App\PES3-Disc.App.csproj') `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $out

if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

$exe = Join-Path $out 'PES3-Disc.exe'
Write-Host ""
Write-Host "Done: $exe"
Write-Host 'Run PES3-Disc.exe - setup wizard, disc scan, decrypt, and play in one app.'
