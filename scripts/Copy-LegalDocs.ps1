# Copies LICENSE and legal notices into a distribution folder.
param(
    [Parameter(Mandatory = $true)]
    [string]$Destination
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
foreach ($name in @('LICENSE', 'LEGAL.md', 'THIRD_PARTY_NOTICES.md', 'PRIVACY.md', 'SECURITY.md')) {
    $src = Join-Path $root $name
    if (Test-Path -LiteralPath $src) {
        Copy-Item -LiteralPath $src -Destination (Join-Path $Destination $name) -Force
    }
}
$guide = Join-Path $root 'docs\USER-LEGAL-GUIDE.md'
if (Test-Path -LiteralPath $guide) {
    $docsDir = Join-Path $Destination 'docs'
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null
    Copy-Item -LiteralPath $guide -Destination (Join-Path $docsDir 'USER-LEGAL-GUIDE.md') -Force
}
