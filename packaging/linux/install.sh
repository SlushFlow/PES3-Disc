#!/usr/bin/env bash
# Install pes3-disc into ~/.local (default).
set -euo pipefail
DEST="${PES3_INSTALL_DIR:-$HOME/.local}"
BIN="$DEST/bin"
SHARE="$DEST/share"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

mkdir -p "$BIN" "$SHARE/applications" "$SHARE/doc/pes3-disc"
install -m 755 "$SCRIPT_DIR/pes3-disc" "$BIN/pes3-disc"
if [[ -f "$SCRIPT_DIR/pes3-disc-dump" ]]; then
  install -m 755 "$SCRIPT_DIR/pes3-disc-dump" "$BIN/pes3-disc-dump"
fi
if [[ -f "$SCRIPT_DIR/pes3-disc.desktop" ]]; then
  sed "s|@BINDIR@|$BIN|g" "$SCRIPT_DIR/pes3-disc.desktop" > "$SHARE/applications/pes3-disc.desktop"
fi
if [[ -f "$SCRIPT_DIR/LINUX.md" ]]; then
  cp "$SCRIPT_DIR/LINUX.md" "$SHARE/doc/pes3-disc/"
fi

echo "Installed to $BIN"
echo "Ensure $BIN is on your PATH, then run: pes3-disc setup \$(which rpcs3)"
