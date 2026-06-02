#!/usr/bin/env bash
# Builds PES3-Disc Avalonia GUI + pes3-disc-dump-linux for linux-x64.
# Expects ps3-disc-dumper cloned and patched by CI (see patch-ps3-disc-dumper-for-linux.sh).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT="$ROOT/dist/linux-x64"
PKG="$ROOT/installer/output"
mkdir -p "$OUT" "$PKG"

echo "==> Restoring solution…"
dotnet restore PES3-Disc.sln

echo "==> Compiling solution (Release)…"
dotnet build PES3-Disc.sln -c Release --no-restore

echo "==> Publishing PES3-Disc GUI (Avalonia, linux-x64)…"
dotnet publish src/PES3-Disc.Avalonia/PES3-Disc.Avalonia.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$OUT" \
  --no-restore

DISC_DUMPER="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj"
if [[ -f "$DISC_DUMPER" ]]; then
  echo "==> Publishing pes3-disc-dump-linux…"
  dotnet publish tools/PES3-Disc.LinuxDump/PES3-Disc.LinuxDump.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -o "$OUT" \
    --no-restore
  echo "Included pes3-disc-dump-linux"
else
  echo "SKIP: external/ps3-disc-dumper not present (retail decrypt not bundled)." >&2
fi

if [[ -f src/PES3-Disc.Cli/PES3-Disc.Cli.csproj ]]; then
  echo "==> Publishing optional pes3-disc-cli…"
  dotnet publish src/PES3-Disc.Cli/PES3-Disc.Cli.csproj \
    -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true \
    -o "$OUT" -p:AssemblyName=pes3-disc-cli --no-restore || true
fi

[[ -f packaging/linux/install.sh ]] && cp packaging/linux/install.sh "$OUT/" && chmod +x "$OUT/install.sh"
[[ -f packaging/linux/pes3-disc.desktop ]] && cp packaging/linux/pes3-disc.desktop "$OUT/"
[[ -f docs/LINUX.md ]] && cp docs/LINUX.md "$OUT/"

ARCHIVE="$PKG/PES3-Disc-linux-x64.tar.gz"
tar -czf "$ARCHIVE" -C "$OUT" .
echo ""
echo "Done: $OUT/PES3-Disc (GUI)"
echo "Archive: $ARCHIVE"
