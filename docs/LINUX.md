# PES3-Disc on Linux

Run PS3 discs in [RPCS3](https://rpcs3.net/) on Linux with the same **PES3 cache** as Windows (DIY copy + retail decrypt to SSD).

## Requirements

- **Linux x64** (Ubuntu 22.04+, Fedora, Arch, etc.)
- **RPCS3** installed (`rpcs3` on `PATH` or `~/.local/share/rpcs3/rpcs3`)
- Disc mounted read-only under **`/media/$USER/…`** or **`/run/media/$USER/…`** (typical udisks layout)
- For **retail decrypt**: compatible drive, `pes3-disc-dump` in the bundle (needs .NET 10 to build), IRD keys

Optional: `rsync` for faster cache copies.

## Install from release

Download **`PES3-Disc-linux-x64.tar.gz`** from [GitHub Releases](https://github.com/SlushFlow/PES3-Disc/releases).

```bash
tar -xzf PES3-Disc-linux-x64.tar.gz
cd PES3-Disc-linux-x64   # or extract into a folder of your choice
./install.sh
export PATH="$HOME/.local/bin:$PATH"
pes3-disc setup "$(which rpcs3)"
```

## Build locally

```bash
git clone https://github.com/SlushFlow/PES3-Disc.git
cd PES3-Disc
git clone --depth 1 https://github.com/13xforever/ps3-disc-dumper.git external/ps3-disc-dumper
./scripts/Apply-Ps3DiscDumperBuildProps.ps1   # needs PowerShell, or copy build/ps3-disc-dumper.Directory.Build.props manually
chmod +x scripts/build-linux.sh
./scripts/build-linux.sh
./dist/linux-x64/pes3-disc setup "$(which rpcs3)"
```

## Commands

| Command | Description |
|---------|-------------|
| `pes3-disc` | Watch mounted volumes (interactive prompts) |
| `pes3-disc scan` | List PS3 volumes once |
| `pes3-disc play 0` | Play index `0` from last scan |
| `pes3-disc decrypt 0` | Decrypt retail disc and play |
| `pes3-disc setup /path/to/rpcs3` | Save RPCS3 path |
| `pes3-disc config` | Show paths and options |

Config file: `~/.config/PES3-Disc/config.json` (same fields as `config.example.json`).

PES3 data: `<rpcs3-install-dir>/PES3/cache` (or custom `DumpCachePath`).

## Disc detection on Linux

PES3-Disc scans:

- `DriveInfo` CD-ROM mounts
- `/proc/mounts` for `/dev/sr*` + udf/iso9660
- `/media/*` and `/run/media/*`

Insert a disc and wait for the desktop to mount it, then run `pes3-disc scan`.

## Retail decrypt on Linux

Uses `pes3-disc-dump --mount /run/media/user/DiscName` (same engine as Windows). Drive access and IRD requirements match [RETAIL-DECRYPT.md](RETAIL-DECRYPT.md).

## Windows vs Linux

| Feature | Windows | Linux |
|---------|---------|-------|
| GUI (`PES3-Disc.exe`) | Yes | CLI only (`pes3-disc`) |
| Installer | Inno Setup | `.tar.gz` + `install.sh` |
| Unified PES3 cache | Yes | Yes |
| DIY staging | Yes | Yes (`rsync` or managed copy) |
