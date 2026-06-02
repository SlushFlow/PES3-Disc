# PES3-Disc on Linux

Full **desktop GUI** (Avalonia, same workflow as Windows) plus a **separate Linux retail dumper** (`pes3-disc-dump-linux`).

## Requirements

- **Linux x64**, .NET 8 runtime (bundled in release tarball)
- **RPCS3** for Linux (`rpcs3` on `PATH` or `~/.local/share/rpcs3/rpcs3`)
- PS3 disc mounted under **`/media/$USER/…`** or **`/run/media/$USER/…`**
- Retail decrypt: `pes3-disc-dump-linux`, compatible BD drive, IRD keys, membership in the **`disk`** group (or run with access to `/dev/sr0`)

Optional: `rsync` for faster cache copies.

## Download

From [GitHub Releases](https://github.com/SlushFlow/PES3-Disc/releases): **`PES3-Disc-linux-x64.tar.gz`**

```bash
tar -xzf PES3-Disc-linux-x64.tar.gz -C ~/.local/opt
cd ~/.local/opt   # folder containing PES3-Disc, pes3-disc-dump-linux, install.sh
./install.sh
export PATH="$HOME/.local/bin:$PATH"
PES3-Disc   # or: pes3-disc
```

First launch opens **Setup** (RPCS3 path, cache options).

## GUI (Avalonia)

Same screens as Windows WPF:

| Screen | Purpose |
|--------|---------|
| **Setup** | RPCS3 path, cache mode, retail decrypt |
| **Home** | Scan drives, **Play** / **Play from cache** / **Decrypt & play** |
| **Stage** | Copy DIY disc to PES3 cache |
| **Decrypt** | Retail decrypt progress |
| **Settings** | Cache path, `pes3-disc-dump-linux` path, IRD folder |

Built with **Avalonia UI** (not WPF — Windows-only).

## Linux retail dump (separate from Windows)

| | Windows | Linux |
|---|---------|-------|
| Tool | `pes3-disc-dump.exe` | **`pes3-disc-dump-linux`** |
| Project | `PES3-Disc.DumpCli` | **`PES3-Disc.LinuxDump`** |
| Drive access | Drive letter + WMI | **`/dev/sr*`** + mounts |

The Linux tool uses the PS3 Disc Dumper **engine** with Linux block-device enumeration (`/proc/sys/dev/cdrom/info`, `/dev/sr*`). It is **not** the same executable as Windows.

Manual CLI example:

```bash
pes3-disc-dump-linux --output /tmp/ps3dump --mount /run/media/$USER/MY_DISC --device /dev/sr0
```

## Headless CLI (optional)

`pes3-disc-cli` may be included for scripting (`scan`, `play`, `decrypt`). The **GUI is the recommended** Linux experience.

## Build from source

```bash
git clone https://github.com/SlushFlow/PES3-Disc.git
cd PES3-Disc
git clone --depth 1 https://github.com/13xforever/ps3-disc-dumper.git external/ps3-disc-dumper
cp build/ps3-disc-dumper.Directory.Build.props external/ps3-disc-dumper/
chmod +x scripts/build-linux.sh scripts/patch-ps3-disc-dumper-for-linux.sh
./scripts/build-linux.sh
```

## Config

`~/.config/PES3-Disc/config.json` — set `DumpCliPath` to your `pes3-disc-dump-linux` if not beside the GUI binary.

PES3 cache: `<rpcs3-dir>/PES3/cache` (same layout as Windows).
