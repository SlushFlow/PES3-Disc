#!/usr/bin/env bash
# Patches cloned ps3-disc-dumper so Ps3DiscDumper builds on Linux (no WmiLight types).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DUMPER="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Dumper.cs"
PROJ="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj"

[[ -f "$DUMPER" ]] || { echo "SKIP: $DUMPER not found"; exit 0; }

python3 <<PY
import re
import sys
from pathlib import Path

dumper = Path(r"$DUMPER")
proj = Path(r"$PROJ")
text = dumper.read_text(encoding="utf-8")

if "#if PES3_LINUX_BUILD" not in text:
    start = re.compile(
        r"(\[SupportedOSPlatform\(\"windows\"\)\]\s*"
        r"private List<\(string path, string name\)> EnumeratePhysicalDrivesWindows\(\)\s*\{\s*)"
        r"if \(!RuntimeInformation\.IsOSPlatform\(OSPlatform\.Windows\)\)\s*"
        r"throw new NotImplementedException\(\"This should never happen, shut up msbuild\"\);",
        re.DOTALL,
    )
    if not start.search(text):
        sys.exit("Dumper.cs WMI block not found — upstream layout changed.")
    text = start.sub(
        r"\1#if PES3_LINUX_BUILD\n        return [];\n#else\n        "
        r"if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))\n            "
        r"throw new NotImplementedException(\"This should never happen, shut up msbuild\");",
        text,
        count=1,
    )
    end = re.compile(
        r"(return \[\.{2} physicalDriveList\.Distinct\(\)\];\s*\n)(\s*\}\s*\n\s*\[SupportedOSPlatform\(\"linux\"\)\])",
        re.DOTALL,
    )
    if not end.search(text):
        sys.exit("Dumper.cs WMI end block not found — upstream layout changed.")
    text = end.sub(r"\1#endif\n\2", text, count=1)
    print("Patched EnumeratePhysicalDrivesWindows for PES3_LINUX_BUILD.")
else:
    print("Dumper.cs WMI block already patched.")

if "#if !PES3_LINUX_BUILD" not in text and re.search(r"^using WmiLight;\s*$", text, re.MULTILINE):
    text = re.sub(
        r"^using WmiLight;\s*$",
        "#if !PES3_LINUX_BUILD\nusing WmiLight;\n#endif",
        text,
        count=1,
        flags=re.MULTILINE,
    )
    print("Wrapped using WmiLight for PES3_LINUX_BUILD.")

dumper.write_text(text, encoding="utf-8")

if proj.is_file():
    ptext = proj.read_text(encoding="utf-8")
    if "WmiLight" in ptext and "IsOSPlatform('Windows')" not in ptext:
        def add_condition(match: re.Match[str]) -> str:
            tag = match.group(0)
            if "Condition=" in tag:
                return tag
            return tag[:-2] + ' Condition="$([MSBuild]::IsOSPlatform(\'Windows\'))" />'

        ptext, n = re.subn(
            r"<PackageReference Include=\"WmiLight\"[^>]*/>",
            add_condition,
            ptext,
            count=1,
        )
        if n == 0:
            sys.exit("Ps3DiscDumper.csproj WmiLight PackageReference not found.")
        proj.write_text(ptext, encoding="utf-8")
        print("Patched Ps3DiscDumper.csproj WmiLight PackageReference.")
    elif "IsOSPlatform('Windows')" in ptext:
        print("Ps3DiscDumper.csproj WmiLight already Windows-only.")
PY

grep -q '#if PES3_LINUX_BUILD' "$DUMPER" || { echo "ERROR: Missing #if PES3_LINUX_BUILD in Dumper.cs"; exit 1; }
grep -q '#if !PES3_LINUX_BUILD' "$DUMPER" || { echo "ERROR: Missing #if !PES3_LINUX_BUILD (using WmiLight guard) in Dumper.cs"; exit 1; }
[[ -f "$PROJ" ]] && grep -q "IsOSPlatform('Windows')" "$PROJ" || { echo "ERROR: WmiLight not Windows-only in Ps3DiscDumper.csproj"; exit 1; }

echo "ps3-disc-dumper Linux patch complete."
