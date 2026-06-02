# PES3-Disc setup: config, startup shortcut, and/or retail decrypt build.
param(
    [switch]$Config,
    [switch]$Startup,
    [switch]$RetailDecrypt,
    [switch]$All
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot

if (-not $Config -and -not $Startup -and -not $RetailDecrypt -and -not $All) {
    $Config = $true
}

function Invoke-Pes3ConfigSetup {
    . (Join-Path $scriptDir 'Ps3DiscRun.ps1')
    Ensure-WinFormsLoaded

    $config = Get-Config
    $current = if ($config) { $config.Rpcs3Path } else { '' }

    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Filter = 'RPCS3 (rpcs3.exe)|rpcs3.exe|All files (*.*)|*.*'
    $dialog.Title = 'Select rpcs3.exe'
    $dialog.FileName = if ($current) { Split-Path $current -Leaf } else { 'rpcs3.exe' }
    if ($current) { $dialog.InitialDirectory = Split-Path $current -Parent }

    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        Write-Host 'Cancelled.'
        exit 1
    }

    $delay = 3
    $noGui = $false
    if ($config) {
        if ($config.ScanDelaySeconds) { $delay = [int]$config.ScanDelaySeconds }
        if ($null -ne $config.UseNoGui) { $noGui = [bool]$config.UseNoGui }
    }

    Save-Config -Rpcs3Path $dialog.FileName -ScanDelaySeconds $delay -UseNoGui $noGui
    Write-Host 'Saved config.json'
    Write-Host "  RPCS3: $($dialog.FileName)"
    if ($Script:Pes3Root) {
        Write-Host "  PES3 folder: $Script:Pes3Root"
    }
}

function Invoke-Pes3StartupSetup {
    $batPath = Join-Path $scriptDir 'Start-PES3-Disc.bat'
    $startup = [Environment]::GetFolderPath('Startup')
    $linkPath = Join-Path $startup 'PES3-Disc.lnk'

    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($linkPath)
    $shortcut.TargetPath = $batPath
    $shortcut.WorkingDirectory = $scriptDir
    $shortcut.WindowStyle = 7
    $shortcut.Description = 'Prompt to run PS3 discs in RPCS3 when inserted'
    $shortcut.Save()

    Write-Host 'Startup shortcut created:'
    Write-Host "  $linkPath"
}

function Invoke-Pes3RetailDecryptSetup {
    $root = $scriptDir
    $submodule = Join-Path $root 'external\ps3-disc-dumper'
    $git = "$env:LOCALAPPDATA\Programs\Git\cmd\git.exe"
    if (-not (Test-Path -LiteralPath $git)) { $git = 'git' }

    Write-Host 'Building retail disc decryptor (pes3-disc-dump.exe)...'

    if (-not (Test-Path -LiteralPath $submodule)) {
        New-Item -ItemType Directory -Path (Split-Path $submodule -Parent) -Force | Out-Null
        & $git clone --depth 1 https://github.com/13xforever/ps3-disc-dumper.git $submodule
        if ($LASTEXITCODE -ne 0) { throw 'Failed to clone ps3-disc-dumper.' }
    }

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw '.NET SDK not found. Install .NET 10 SDK from https://dotnet.microsoft.com/download'
    }

    $outDir = Join-Path $root 'tools'
    $proj = Join-Path $root 'tools\PES3-Disc.DumpCli\PES3-Disc.DumpCli.csproj'
    & dotnet publish $proj -c Release -r win-x64 --self-contained false -o $outDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    $exe = Join-Path $outDir 'pes3-disc-dump.exe'
    if (-not (Test-Path -LiteralPath $exe)) { throw "Missing $exe" }
    Write-Host "Built: $exe"
}

if ($All -or $Config) { Invoke-Pes3ConfigSetup }
if ($All -or $Startup) { Invoke-Pes3StartupSetup }
if ($All -or $RetailDecrypt) { Invoke-Pes3RetailDecryptSetup }

Write-Host ''
Write-Host 'Done. Run Start-PES3-Disc.bat to enable disc prompts.'
