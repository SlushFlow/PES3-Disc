#!/usr/bin/env bash
# Patches cloned ps3-disc-dumper so Ps3DiscDumper builds on Linux (no WmiLight types).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DUMPER="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Dumper.cs"
PROJ="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj"

[[ -f "$DUMPER" ]] || { echo "SKIP: $DUMPER not found"; exit 0; }

if grep -q '#if PES3_LINUX_BUILD' "$DUMPER"; then
  echo "Dumper.cs already patched for Linux."
  exit 0
fi

python3 <<PY
from pathlib import Path

dumper = Path(r"$DUMPER")
text = dumper.read_text(encoding="utf-8")

start = '''    [SupportedOSPlatform("windows")]
    private List<(string path, string name)> EnumeratePhysicalDrivesWindows()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new NotImplementedException("This should never happen, shut up msbuild");'''

replacement = '''    [SupportedOSPlatform("windows")]
    private List<(string path, string name)> EnumeratePhysicalDrivesWindows()
    {
#if PES3_LINUX_BUILD
        return [];
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new NotImplementedException("This should never happen, shut up msbuild");'''

if start not in text:
    raise SystemExit("Dumper.cs WMI block not found — upstream layout changed.")

text = text.replace(start, replacement, 1)

end_marker = '''        return [.. physicalDriveList.Distinct()];
    }

    [SupportedOSPlatform("linux")]'''

end_replacement = '''        return [.. physicalDriveList.Distinct()];
#endif
    }

    [SupportedOSPlatform("linux")]'''

if end_marker not in text:
    raise SystemExit("Dumper.cs WMI end block not found.")

text = text.replace(end_marker, end_replacement, 1)
dumper.write_text(text, encoding="utf-8")
print("Patched Dumper.cs for PES3_LINUX_BUILD.")
PY

if [[ -f "$PROJ" ]] && grep -q 'WmiLight' "$PROJ" && ! grep -q "IsOSPlatform('Windows')" "$PROJ"; then
  sed -i 's|<PackageReference Include="WmiLight" Version="7.2.0" />|<PackageReference Include="WmiLight" Version="7.2.0" Condition="$([MSBuild]::IsOSPlatform('\''Windows'\''))" />|' "$PROJ"
fi

echo "ps3-disc-dumper Linux patch complete."
