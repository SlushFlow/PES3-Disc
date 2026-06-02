# Builds PES3-Disc.exe (self-contained GUI) into dist\
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$git = "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe"
if (-not (Test-Path -LiteralPath $git)) { $git = 'git' }

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw @'
.NET SDK not found.

Install .NET 8 SDK (minimum) or .NET 10 SDK:
  https://dotnet.microsoft.com/download

Then install the WPF workload if needed:
  dotnet workload install microsoft-net-sdk-wpf
  dotnet workload restore
'@
}

Write-Host 'Installed SDKs:'
& dotnet --list-sdks
Write-Host ''

# Ensure WPF / Windows desktop workload
Write-Host 'Restoring workloads (WPF)...'
& dotnet workload restore (Join-Path $root 'src\PES3-Disc.App\PES3-Disc.App.csproj')
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Trying: dotnet workload install microsoft-net-sdk-wpf' -ForegroundColor Yellow
    & dotnet workload install microsoft-net-sdk-wpf
}

$out = Join-Path $root 'dist'
$log = Join-Path $root 'dist-publish.log'
if (Test-Path -LiteralPath $out) {
    Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Path $out -Force | Out-Null

Write-Host "Publishing PES3-Disc.exe to $out ..."
Write-Host "(Full log: $log)"
Write-Host ''

$publishArgs = @(
    'publish',
    (Join-Path $root 'src\PES3-Disc.App\PES3-Disc.App.csproj'),
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o', $out
)

& dotnet @publishArgs 2>&1 | Tee-Object -FilePath $log

if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host '======== dotnet publish failed ========' -ForegroundColor Red
    Write-Host 'Last lines from the log:' -ForegroundColor Yellow
    if (Test-Path -LiteralPath $log) {
        Get-Content -LiteralPath $log -Tail 35 | ForEach-Object { Write-Host $_ }
    }
    Write-Host ''
    Write-Host 'Common fixes:' -ForegroundColor Cyan
    Write-Host '  1. Install .NET 8 SDK (or newer) from https://dotnet.microsoft.com/download'
    Write-Host '  2. Run: dotnet workload install microsoft-net-sdk-wpf'
    Write-Host '  3. Run: dotnet workload restore'
    Write-Host '  4. Open PES3-Disc.sln in Visual Studio 2022 and build once (installs missing components)'
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

# Optional: build retail decrypt CLI (needs ps3-disc-dumper + .NET 10 SDK)
$submodule = Join-Path $root 'external\ps3-disc-dumper'
$discDumperProj = Join-Path $submodule 'Ps3DiscDumper\Ps3DiscDumper.csproj'
$dumpProj = Join-Path $root 'tools\PES3-Disc.DumpCli\PES3-Disc.DumpCli.csproj'
$sdks = & dotnet --list-sdks 2>&1 | Out-String
$hasNet10 = $sdks -match '10\.'

if ((Test-Path -LiteralPath $dumpProj) -and $hasNet10) {
    if (-not (Test-Path -LiteralPath $discDumperProj)) {
        Write-Host 'Cloning ps3-disc-dumper for pes3-disc-dump.exe...'
        New-Item -ItemType Directory -Path (Split-Path $submodule -Parent) -Force | Out-Null
        & $git clone --depth 1 https://github.com/13xforever/ps3-disc-dumper.git $submodule
    }
    if (Test-Path -LiteralPath $discDumperProj) {
        Write-Host 'Building pes3-disc-dump.exe (retail decrypt)...'
        & dotnet publish $dumpProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $out
        if ($LASTEXITCODE -eq 0) {
            Write-Host 'Included pes3-disc-dump.exe in dist folder.'
        }
        else {
            $msg = 'pes3-disc-dump.exe build failed; retail decrypt needs .NET 10 SDK.'
            if ($env:GITHUB_ACTIONS -eq 'true' -or $env:CI -eq 'true') {
                throw $msg
            }
            Write-Host "WARNING: $msg" -ForegroundColor Yellow
        }
    }
}
elseif (Test-Path -LiteralPath $dumpProj) {
    Write-Host 'SKIP: pes3-disc-dump.exe needs .NET 10 SDK (install alongside .NET 8 for retail decrypt).' -ForegroundColor Yellow
}

$exe = Join-Path $out 'PES3-Disc.exe'
Write-Host ''
Write-Host "Done: $exe"
if (Test-Path -LiteralPath (Join-Path $out 'pes3-disc-dump.exe')) {
    Write-Host 'Retail decrypt: pes3-disc-dump.exe is in dist\'
}
else {
    Write-Host 'Retail decrypt: install .NET 10 SDK and re-run Build-App.ps1 to add pes3-disc-dump.exe'
}
Write-Host 'Run PES3-Disc.exe - setup wizard, disc scan, decrypt, and play in one app.'

if (-not (Test-Path -LiteralPath $exe)) { exit 1 }
exit 0
