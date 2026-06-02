# Copies build/ps3-disc-dumper.Directory.Build.props into the cloned dumper repo.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$submodule = Join-Path $root 'external\ps3-disc-dumper'
$src = Join-Path $root 'build\ps3-disc-dumper.Directory.Build.props'
$dest = Join-Path $submodule 'Directory.Build.props'

if (-not (Test-Path -LiteralPath $submodule)) {
    Write-Host 'SKIP: external\ps3-disc-dumper not present'
    exit 0
}
if (-not (Test-Path -LiteralPath $src)) {
    throw "Missing template: $src"
}
Copy-Item -LiteralPath $src -Destination $dest -Force
Write-Host "Applied upstream warning suppressions: $dest"
