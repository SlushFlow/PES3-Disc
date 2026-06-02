# Creates two temporary PS3 disc layouts for PES3-Disc testing (DIY decrypted + retail encrypted).
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
. (Join-Path $here 'New-ParamSfo.ps1')

function New-EncryptedEboot {
    param([string]$Path)
    # SCE/SELF-style header stub (retail encrypted EBOOT signature used by PES3-Disc detection).
    $bytes = [byte[]](
        0x53, 0x43, 0x45, 0x00, 0x00, 0x00, 0x02, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    )
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [IO.File]::WriteAllBytes($Path, $bytes)
}

function New-DecryptedEboot {
    param([string]$Path)
    # ELF header stub (decrypted / DIY EBOOT).
    $bytes = [byte[]](
        0x7F, 0x45, 0x4C, 0x46, 0x01, 0x02, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    )
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [IO.File]::WriteAllBytes($Path, $bytes)
}

# --- DIY decrypted disc (burned / mounted layout) ---
$diyRoot = Join-Path $here 'diy-demo-disc'
$diyPs3 = Join-Path $diyRoot 'PS3_GAME'
$diyUsr = Join-Path $diyPs3 'USRDIR'
New-Item -ItemType Directory -Path $diyUsr -Force | Out-Null
New-DecryptedEboot -Path (Join-Path $diyUsr 'EBOOT.BIN')
New-ParamSfoFile -Path (Join-Path $diyPs3 'PARAM.SFO') -TitleId 'BLUS99991' -Title 'PES3 DIY Test Disc'
Set-Content -LiteralPath (Join-Path $diyRoot 'PS3_DISC.SFB') -Value 'PES3-DIY-TEST' -Encoding ASCII -NoNewline
Set-Content -LiteralPath (Join-Path $diyUsr 'readme.txt') -Value 'Temporary DIY test layout for PES3-Disc.' -Encoding UTF8

# --- Retail encrypted disc (official-style layout) ---
$retailRoot = Join-Path $here 'retail-encrypted-disc'
$retailPs3 = Join-Path $retailRoot 'PS3_GAME'
$retailUsr = Join-Path $retailPs3 'USRDIR'
New-Item -ItemType Directory -Path $retailUsr -Force | Out-Null
New-EncryptedEboot -Path (Join-Path $retailUsr 'EBOOT.BIN')
New-ParamSfoFile -Path (Join-Path $retailPs3 'PARAM.SFO') -TitleId 'BLUS99992' -Title 'PES3 Retail Test Disc' -DiscId 'PES3-RETAIL-TEST'
Set-Content -LiteralPath (Join-Path $retailRoot 'PS3_DISC.SFB') -Value 'PES3-RETAIL-TEST' -Encoding ASCII -NoNewline
New-Item -ItemType Directory -Path (Join-Path $retailRoot 'BDMV\STREAM') -Force | Out-Null

Write-Host "Built test fixtures:"
Write-Host "  DIY (decrypted):  $diyRoot"
Write-Host "  Retail (encrypt): $retailRoot"
