# Installed by PES3-Disc-Setup.exe before first launch (requires internet).
$ErrorActionPreference = 'Stop'

$dotnet8Url = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe'
$dotnet10Url = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe'
$temp = Join-Path $env:TEMP 'pes3-disc-install'

function Test-DotNetDesktopInstalled {
    param([string]$Major)
    try {
        $lines = & dotnet --list-runtimes 2>$null
        if (-not $lines) { return $false }
        return @($lines | Where-Object { $_ -match "Microsoft\.WindowsDesktop\.App\s+$Major\." }).Count -gt 0
    }
    catch {
        return $false
    }
}

function Install-DotNetRuntime {
    param(
        [string]$Url,
        [string]$Name,
        [string]$Major
    )

    if (Test-DotNetDesktopInstalled $Major) {
        return
    }

    New-Item -ItemType Directory -Path $temp -Force | Out-Null
    $file = Join-Path $temp ($Name + '.exe')
    Invoke-WebRequest -Uri $Url -OutFile $file -UseBasicParsing

    $proc = Start-Process -FilePath $file -ArgumentList '/install', '/quiet', '/norestart' -Wait -PassThru
    $code = $proc.ExitCode
    if ($code -eq 0 -or $code -eq 1638 -or $code -eq 3010) {
        return
    }
    throw "$Name installer failed with exit code $code"
}

Install-DotNetRuntime -Url $dotnet8Url -Name 'dotnet8-desktop-runtime' -Major '8'
Install-DotNetRuntime -Url $dotnet10Url -Name 'dotnet10-desktop-runtime' -Major '10'
