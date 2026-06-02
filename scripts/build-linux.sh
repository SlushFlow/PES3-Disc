#!/usr/bin/env bash
# Builds PES3-Disc Avalonia GUI + pes3-disc-dump-linux for linux-x64.
# Expects ps3-disc-dumper cloned and patched by CI (see patch-ps3-disc-dumper-for-linux.sh).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"
OUT="$ROOT/dist/linux-x64"
PKG="$ROOT/installer/output"
mkdir -p "$OUT" "$PKG"

# Linux bundle only — skip net8.0-windows WPF (PES3-Disc.App) and Windows DumpCli.
LINUX_PROJECTS=(
  "src/PES3-Disc.Avalonia/PES3-Disc.Avalonia.csproj"
  "src/PES3-Disc.Cli/PES3-Disc.Cli.csproj"
)
DISC_DUMPER="$ROOT/external/ps3-disc-dumper/Ps3DiscDumper/Ps3DiscDumper.csproj"
LINUX_DUMP_PROJ="tools/PES3-Disc.LinuxDump/PES3-Disc.LinuxDump.csproj"
LINUX_DUMP_PROPS=()
if [[ -f "$DISC_DUMPER" ]]; then
  LINUX_PROJECTS+=("$LINUX_DUMP_PROJ")
  LINUX_DUMP_PROPS=(-p:DefineConstants="PES3_LINUX_BUILD")
fi

for proj in "${LINUX_PROJECTS[@]}"; do
  echo "==> Restore $proj"
  extra=()
  [[ "$proj" == "$LINUX_DUMP_PROJ" ]] && extra=("${LINUX_DUMP_PROPS[@]}")
  dotnet restore "$proj" -r linux-x64 "${extra[@]}"
done

for proj in "${LINUX_PROJECTS[@]}"; do
  echo "==> Build $proj"
  extra=()
  [[ "$proj" == "$LINUX_DUMP_PROJ" ]] && extra=("${LINUX_DUMP_PROPS[@]}")
  dotnet build "$proj" -c Release -r linux-x64 --no-restore "${extra[@]}"
done

echo "==> Publishing PES3-Disc GUI (Avalonia, linux-x64)…"
dotnet publish src/PES3-Disc.Avalonia/PES3-Disc.Avalonia.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$OUT" \
  --no-restore

if [[ -f "$DISC_DUMPER" ]]; then
  echo "==> Publishing pes3-disc-dump-linux…"
  dotnet publish tools/PES3-Disc.LinuxDump/PES3-Disc.LinuxDump.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:DefineConstants="PES3_LINUX_BUILD" \
    -o "$OUT" \
    --no-restore
  echo "Included pes3-disc-dump-linux"
else
  echo "SKIP: external/ps3-disc-dumper not present (retail decrypt not bundled)." >&2
fi

if [[ -f src/PES3-Disc.Cli/PES3-Disc.Cli.csproj ]]; then
  echo "==> Publishing optional pes3-disc-cli…"
  CLI_STAGE="$ROOT/dist/cli-publish"
  rm -rf "$CLI_STAGE"
  dotnet publish src/PES3-Disc.Cli/PES3-Disc.Cli.csproj \
    -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true \
    -o "$CLI_STAGE"
  cp -f "$CLI_STAGE/pes3-disc-cli" "$OUT/"
  rm -rf "$CLI_STAGE"
fi

[[ -f packaging/linux/install.sh ]] && cp packaging/linux/install.sh "$OUT/" && chmod +x "$OUT/install.sh"
[[ -f packaging/linux/pes3-disc.desktop ]] && cp packaging/linux/pes3-disc.desktop "$OUT/"
[[ -f docs/LINUX.md ]] && cp docs/LINUX.md "$OUT/"

ARCHIVE="$PKG/PES3-Disc-linux-x64.tar.gz"
tar -czf "$ARCHIVE" -C "$OUT" .
echo ""
echo "Done: $OUT/PES3-Disc (GUI)"
echo "Archive: $ARCHIVE"
