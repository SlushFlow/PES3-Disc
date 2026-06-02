#!/usr/bin/env bash
# Install pes3-disc into ~/.local (default).
set -euo pipefail
DEST="${PES3_INSTALL_DIR:-$HOME/.local}"
BIN="$DEST/bin"
SHARE="$DEST/share"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

mkdir -p "$BIN" "$SHARE/applications" "$SHARE/doc/pes3-disc" "$SHARE/icons/hicolor/256x256/apps"
install -m 755 "$SCRIPT_DIR/PES3-Disc" "$BIN/PES3-Disc"
ln -sf PES3-Disc "$BIN/pes3-disc" 2>/dev/null || true
if [[ -f "$SCRIPT_DIR/PES3-Disc.png" ]]; then
  install -m 644 "$SCRIPT_DIR/PES3-Disc.png" "$SHARE/icons/hicolor/256x256/apps/pes3-disc.png"
fi
if [[ -f "$SCRIPT_DIR/pes3-disc-dump-linux" ]]; then
  install -m 755 "$SCRIPT_DIR/pes3-disc-dump-linux" "$BIN/pes3-disc-dump-linux"
fi
if [[ -f "$SCRIPT_DIR/pes3-disc.desktop" ]]; then
  sed "s|@BINDIR@|$BIN|g" "$SCRIPT_DIR/pes3-disc.desktop" > "$SHARE/applications/pes3-disc.desktop"
fi
if [[ -f "$SCRIPT_DIR/LINUX.md" ]]; then
  cp "$SCRIPT_DIR/LINUX.md" "$SHARE/doc/pes3-disc/"
fi

echo "Installed to $BIN"
echo "Ensure $BIN is on your PATH, then run: pes3-disc setup \$(which rpcs3)"
