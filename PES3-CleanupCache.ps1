# Waits for RPCS3 to exit, then deletes ephemeral decrypt folders (not dev_hdd0 / saves).
param(
    [Parameter(Mandatory = $true)]
    [int]$ProcessId,

    [Parameter(Mandatory = $true)]
    [string]$PathsFile
)

$ErrorActionPreference = 'SilentlyContinue'

try {
    Wait-Process -Id $ProcessId -ErrorAction SilentlyContinue
}
catch { }

# Let RPCS3 release file handles on the decrypted EBOOT / game folder.
Start-Sleep -Seconds 4

if (-not (Test-Path -LiteralPath $PathsFile)) { exit 0 }

try {
    $paths = Get-Content -LiteralPath $PathsFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
catch {
    exit 1
}

foreach ($dir in $paths) {
    if (-not $dir -or -not (Test-Path -LiteralPath $dir)) { continue }
    try {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction Stop
    }
    catch { }
}

Remove-Item -LiteralPath $PathsFile -Force -ErrorAction SilentlyContinue
