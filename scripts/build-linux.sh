#!/usr/bin/env bash
# Builds pes3-disc CLI (+ optional pes3-disc-dump) for linux-x64.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT="$ROOT/dist/linux-x64"
PKG="$ROOT/installer/output"
mkdir -p "$OUT" "$PKG"

echo "==> Publishing PES3-Disc CLI (linux-x64)…"
dotnet publish src/PES3-Disc.Cli/PES3-Disc.Cli.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$OUT"

DISC_DUMPER="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj"
if [[ -f "$DISC_DUMPER" ]]; then
  echo "==> Publishing pes3-disc-dump (linux-x64)…"
  if dotnet publish tools/PES3-Disc.DumpCli/PES3-Disc.DumpCli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$OUT"; then
    echo "Included pes3-disc-dump in $OUT"
  else
    echo "WARNING: pes3-disc-dump build failed (retail decrypt unavailable on this build)." >&2
  fi
else
  echo "SKIP: external/ps3-disc-dumper not present (retail decrypt not bundled)." >&2
fi

if [[ -f packaging/linux/install.sh ]]; then
  cp packaging/linux/install.sh "$OUT/"
  chmod +x "$OUT/install.sh"
fi
if [[ -f packaging/linux/pes3-disc.desktop ]]; then
  cp packaging/linux/pes3-disc.desktop "$OUT/"
fi
if [[ -f docs/LINUX.md ]]; then
  cp docs/LINUX.md "$OUT/"
fi

ARCHIVE="$PKG/PES3-Disc-linux-x64.tar.gz"
tar -czf "$ARCHIVE" -C "$OUT" .
echo ""
echo "Done: $OUT"
echo "Archive: $ARCHIVE"
