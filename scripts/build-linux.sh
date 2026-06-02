#!/usr/bin/env bash
# Builds PES3-Disc Avalonia GUI + pes3-disc-dump-linux for linux-x64.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT="$ROOT/dist/linux-x64"
PKG="$ROOT/installer/output"
mkdir -p "$OUT" "$PKG"

if [[ -d external/ps3-disc-dumper ]]; then
  cp -f build/ps3-disc-dumper.Directory.Build.props external/ps3-disc-dumper/Directory.Build.props
  chmod +x scripts/patch-ps3-disc-dumper-for-linux.sh
  ./scripts/patch-ps3-disc-dumper-for-linux.sh
  # Close #else for WMI stub if patch added opening #else
  if grep -q '#if PES3_LINUX_BUILD' external/ps3-disc-dumper/Ps3DiscDumper/Dumper.cs && ! grep -q '#endif' external/ps3-disc-dumper/Ps3DiscDumper/Dumper.cs; then
    sed -i '/return \[.. physicalDriveList.Distinct()\];/a #endif' external/ps3-disc-dumper/Ps3DiscDumper/Dumper.cs || true
  fi
fi

echo "==> Publishing PES3-Disc GUI (Avalonia, linux-x64)…"
dotnet publish src/PES3-Disc.Avalonia/PES3-Disc.Avalonia.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$OUT"

DISC_DUMPER="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj"
if [[ -f "$DISC_DUMPER" ]]; then
  echo "==> Publishing pes3-disc-dump-linux…"
  if dotnet publish tools/PES3-Disc.LinuxDump/PES3-Disc.LinuxDump.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$OUT"; then
    echo "Included pes3-disc-dump-linux"
  else
    echo "WARNING: pes3-disc-dump-linux build failed (retail decrypt unavailable)." >&2
  fi
else
  echo "SKIP: clone ps3-disc-dumper for retail decrypt on Linux." >&2
fi

# Optional headless CLI
if [[ -f src/PES3-Disc.Cli/PES3-Disc.Cli.csproj ]]; then
  dotnet publish src/PES3-Disc.Cli/PES3-Disc.Cli.csproj \
    -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true \
    -o "$OUT" -p:AssemblyName=pes3-disc-cli 2>/dev/null || true
fi

[[ -f packaging/linux/install.sh ]] && cp packaging/linux/install.sh "$OUT/" && chmod +x "$OUT/install.sh"
[[ -f packaging/linux/pes3-disc.desktop ]] && cp packaging/linux/pes3-disc.desktop "$OUT/"
[[ -f docs/LINUX.md ]] && cp docs/LINUX.md "$OUT/"

ARCHIVE="$PKG/PES3-Disc-linux-x64.tar.gz"
tar -czf "$ARCHIVE" -C "$OUT" .
echo ""
echo "Done: $OUT/PES3-Disc (GUI)"
echo "Archive: $ARCHIVE"
