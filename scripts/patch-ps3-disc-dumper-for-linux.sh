#!/usr/bin/env bash
# Patches ps3-disc-dumper so Ps3DiscDumper builds on Linux (stubs WMI).
set -euo pipefail
DUMPER="${1:-external/ps3-disc-dumper/Ps3DiscDumper/Dumper.cs}"
PROJ="${2:-external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj}"

[[ -f "$DUMPER" ]] || exit 0

if grep -q 'PES3_LINUX_BUILD' "$DUMPER" 2>/dev/null; then
  echo "Dumper.cs already patched."
  exit 0
fi

# Stub WMI method body for Linux builds (PES3_LINUX_BUILD defined via Directory.Build.props).
perl -i -0pe 's/(\[SupportedOSPlatform\("windows"\)\]\s+private List<\(string path, string name\)> EnumeratePhysicalDrivesWindows\(\)\s+\{\s+)if \(!RuntimeInformation)/$1#if PES3_LINUX_BUILD\n        return [];\n#else\n        if (!RuntimeInformation/s' "$DUMPER" || true

if ! grep -q '#if PES3_LINUX_BUILD' "$DUMPER"; then
  echo "WARN: Could not patch Dumper.cs automatically." >&2
fi

if [[ -f "$PROJ" ]] && ! grep -q "IsOSPlatform('Windows')" "$PROJ"; then
  sed -i 's|<PackageReference Include="WmiLight" Version="7.2.0" />|<PackageReference Include="WmiLight" Version="7.2.0" Condition="$([MSBuild]::IsOSPlatform('\''Windows'\''))" />|' "$PROJ"
fi

echo "Patched ps3-disc-dumper for Linux."
