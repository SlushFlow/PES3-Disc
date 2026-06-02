# PES3-Disc bootstrap installer (no Inno Setup required).
# Installs .NET 8 + 10 Desktop Runtimes and copies the app to Program Files.
# Run as Administrator.

#Requires -RunAsAdministrator

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$dist = Join-Path $repoRoot 'dist'
$installDir = Join-Path $env:ProgramFiles 'PES3-Disc'
$dotnet8Url = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe'
$dotnet10Url = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe'
$temp = Join-Path $env:TEMP 'pes3-disc-install'

function Test-DotNetDesktopInstalled {
    param([string]$Major)
    try {
        $lines = & dotnet --list-runtimes 2>$null
        if (-not $lines) { return $false }
        return $lines | Where-Object { $_ -match "Microsoft\.WindowsDesktop\.App\s+$Major\." }
    }
    catch {
        return $false
    }
}

function Install-DotNetRuntime {
    param(
        [string]$Url,
        [string]$Name
    )

    if ($Name -match '8' -and (Test-DotNetDesktopInstalled '8')) {
        Write-Host "Already installed: .NET 8 Desktop Runtime"
        return
    }
    if ($Name -match '10' -and (Test-DotNetDesktopInstalled '10')) {
        Write-Host "Already installed: .NET 10 Desktop Runtime"
        return
    }

    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $file = Join-Path $temp ($Name + '.exe')
    Write-Host "Downloading $Name..."
    Invoke-WebRequest -Uri $Url -OutFile $file -UseBasicParsing

    Write-Host "Installing $Name..."
    $proc = Start-Process -FilePath $file -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
  $code = $proc.ExitCode
    if ($code -eq 0 -or $code -eq 1638 -or $code -eq 3010) {
        Write-Host "OK: $Name (exit $code)"
    }
    else {
        throw "$Name installer failed with exit code $code"
    }
}

Write-Host 'PES3-Disc installer'
Write-Host '===================='
Write-Host ''

if (-not (Test-Path -LiteralPath (Join-Path $dist 'PES3-Disc.exe'))) {
    Write-Host "Missing $dist\PES3-Disc.exe"
    Write-Host 'Build first: powershell -File Build-App.ps1'
    exit 1
}

Install-DotNetRuntime -Url $dotnet8Url -Name 'dotnet8-desktop-runtime'
Install-DotNetRuntime -Url $dotnet10Url -Name 'dotnet10-desktop-runtime'

Write-Host ''
Write-Host "Installing PES3-Disc to $installDir ..."
if (Test-Path -LiteralPath $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}
New-Item -ItemType Directory -Path $installDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $dist '*') -Destination $installDir -Recurse -Force

$shell = New-Object -ComObject WScript.Shell
$startMenu = [Environment]::GetFolderPath('Programs')
$shortcutDir = Join-Path $startMenu 'PES3-Disc'
New-Item -ItemType Directory -Path $shortcutDir -Force | Out-Null
$lnk = Join-Path $shortcutDir 'PES3-Disc.lnk'
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = Join-Path $installDir 'PES3-Disc.exe'
$sc.WorkingDirectory = $installDir
$sc.Description = 'PS3 discs in RPCS3'
$sc.Save()

Write-Host ''
Write-Host "Installed to: $installDir"
Write-Host "Start menu: $lnk"
Write-Host ''
$launch = Read-Host 'Launch PES3-Disc now? (Y/n)'
if ($launch -ne 'n' -and $launch -ne 'N') {
    Start-Process -FilePath (Join-Path $installDir 'PES3-Disc.exe')
}
