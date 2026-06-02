# Builds a minimal valid PARAM.SFO with UTF-8 string keys (for PES3-Disc test fixtures).

function New-ParamSfoFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$TitleId,
        [Parameter(Mandatory = $true)]
        [string]$Title,
        [string]$Category = 'GD',
        [string]$DiscId = 'PES3-TEST-DISC'
    )

    $entries = [ordered]@{
        TITLE           = $Title
        TITLE_ID        = $TitleId
        CATEGORY        = $Category
        DISC_ID         = $DiscId
        PS3_SYSTEM_VER  = '03.0000'
    }

    $headerSize = 0x14
    $keyCount = $entries.Count
    $keyTableSize = $keyCount * 16

    $nameBytes = New-Object System.Collections.Generic.List[byte]
    $nameOffsets = @{}
    foreach ($key in $entries.Keys) {
        $nameOffsets[$key] = $headerSize + $keyTableSize + $nameBytes.Count
        $nameBytes.AddRange([Text.Encoding]::ASCII.GetBytes($key))
        $nameBytes.Add(0)
    }
    while (($nameBytes.Count % 4) -ne 0) { $nameBytes.Add(0) }

    $dataTableOffset = $headerSize + $keyTableSize + $nameBytes.Count
    $dataBytes = New-Object System.Collections.Generic.List[byte]
    $keyTable = New-Object System.Collections.Generic.List[byte]

    foreach ($key in $entries.Keys) {
        $value = [Text.Encoding]::UTF8.GetBytes($entries[$key])
        $dataOff = $dataBytes.Count
        $dataBytes.AddRange($value)
        $dataBytes.Add(0)
        while (($dataBytes.Count % 4) -ne 0) { $dataBytes.Add(0) }

        $len = $value.Length + 1
        $entry = New-Object byte[] 16
        [BitConverter]::GetBytes([uint16]$nameOffsets[$key]).CopyTo($entry, 0)
        [BitConverter]::GetBytes([uint16]0x0204).CopyTo($entry, 2)
        [BitConverter]::GetBytes([uint32]$len).CopyTo($entry, 4)
        [BitConverter]::GetBytes([uint32]$len).CopyTo($entry, 8)
        [BitConverter]::GetBytes([uint32]$dataOff).CopyTo($entry, 12)
        $keyTable.AddRange($entry)
    }

    $file = New-Object System.Collections.Generic.List[byte]
    foreach ($b in @(0, 0x50, 0x53, 0x46)) { $file.Add([byte]$b) }
    foreach ($b in [BitConverter]::GetBytes([uint32]0x101)) { $file.Add($b) }
    foreach ($b in [BitConverter]::GetBytes([uint32]$keyCount)) { $file.Add($b) }
    foreach ($b in [BitConverter]::GetBytes([uint32]$headerSize)) { $file.Add($b) }
    foreach ($b in [BitConverter]::GetBytes([uint32]$dataTableOffset)) { $file.Add($b) }
    $file.AddRange($keyTable)
    $file.AddRange($nameBytes)
    $file.AddRange($dataBytes)

    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [IO.File]::WriteAllBytes($Path, $file.ToArray())
}
