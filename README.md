# PES3-Disc

Play PS3 discs in [RPCS3](https://rpcs3.net/) from your PC. Insert a disc (or mount a PS3-style folder), and PES3-Disc helps you copy or decrypt the game, then launch RPCS3.

**Download:** [GitHub Releases](https://github.com/SlushFlow/PES3-Disc/releases)

| Platform | What to download |
|----------|------------------|
| **Windows 10/11** | `PES3-Disc-Setup.exe` (recommended) or portable ZIP |
| **Linux (64-bit)** | `PES3-Disc-linux-x64.tar.gz` — see [Linux guide](docs/LINUX.md) |

PES3-Disc is not made by Sony, PlayStation, or the RPCS3 team.

---

## What you need

### Operating system

| Platform | Supported |
|----------|-----------|
| **Windows** | 64-bit **Windows 10** or **Windows 11** |
| **Linux** | 64-bit desktop distro with a desktop environment (see [docs/LINUX.md](docs/LINUX.md)) |

### PC specs (practical minimum)

These are guidelines for a smooth experience, not official RPCS3 requirements.

| Component | Recommendation |
|-----------|----------------|
| **CPU** | 6+ cores, recent Intel or AMD (RPCS3 is CPU-heavy) |
| **RAM** | **16 GB** minimum; **32 GB** if you run other apps while emulating |
| **Storage** | **SSD strongly recommended** for cache/decrypt (see below) |
| **Free disk space** | **20–50 GB+** per large retail game (decrypt copy); smaller for DIY burns |
| **GPU** | Whatever RPCS3 needs for your games (Vulkan-capable) |

The Windows installer can download **.NET 8** and **.NET 10** runtimes during setup (internet required). The portable ZIP assumes you already have them.

### Software you must install yourself

1. **[RPCS3](https://rpcs3.net/)** — the PS3 emulator (PES3-Disc only launches games through it).
2. **Firmware & keys** — follow RPCS3’s official setup (PES3-Disc does not include these).
3. **Legal acceptance** — the app asks you to confirm you own the disc and will not redistribute files before play or decrypt.

### Disc drive

| Disc type | Drive needed |
|-----------|----------------|
| **DIY / burned / mounted folder** | Any drive or mount that shows `PS3_GAME` files in Explorer (Windows) or your file manager (Linux) |
| **Official retail PS3 Blu-ray** | A **[Blu-ray drive compatible with PS3 disc dumping](https://rpcs3.net/quickstart#dumping_drives)** — many common PC drives **cannot** read PS3 discs |

Retail decrypt also needs **internet** (for IRD keys) and can take **30–90+ minutes** the first time per disc, depending on drive speed and game size.

---

## What discs work?

| Disc | What happens |
|------|----------------|
| **DIY or burned disc** with a normal `PS3_GAME` folder and **decrypted** `EBOOT.BIN` | Works — app copies to cache, then plays in RPCS3 |
| **Mounted folder** (dump, ISO extract, etc.) with the same layout | Same as DIY |
| **Official retail PS3 disc** | Works **after decrypt** — needs a compatible drive; second insert uses **Play from library** (decrypt once, kept on SSD) |
| **Retail disc + wrong drive** | PC cannot read the disc — upgrade the drive or dump the game elsewhere first |

Your disc should look like this on the drive letter:

```
X:\
├── PS3_DISC.SFB          (often present)
└── PS3_GAME\
    ├── PARAM.SFO
    └── USRDIR\
        └── EBOOT.BIN     (decrypted for DIY; encrypted on retail until decrypt)
```

More detail: [docs/DISC-COMPATIBILITY.md](docs/DISC-COMPATIBILITY.md)

---

## Quick start (Windows)

1. **Install RPCS3** and complete its first-time setup (firmware, etc.).
2. **Download and run** [PES3-Disc-Setup.exe](https://github.com/SlushFlow/PES3-Disc/releases) from Releases.
3. On **first launch**, the setup wizard asks for:
   - Path to `rpcs3.exe`
   - Cache and backup options (defaults are fine for most users)
4. **Insert your disc** (or mount your PS3 folder).
5. On the home screen, choose:
   - **Play** — DIY disc or already-decrypted cache
   - **Decrypt & play** — official retail disc (first time)
   - **Play from library** — retail disc you decrypted before (instant, from SSD)

### Run at Windows login (optional)

In the app **Settings**, enable run at startup — or use the installer’s startup option if offered.

---

## Quick start (Linux)

1. Install **RPCS3 for Linux**.
2. Download **`PES3-Disc-linux-x64.tar.gz`**, extract, and run `./install.sh` (see [docs/LINUX.md](docs/LINUX.md)).
3. Add `~/.local/bin` to your `PATH`, launch **PES3-Disc**.
4. Complete setup, insert or mount the disc under `/media/...` or `/run/media/...`.
5. For **retail decrypt**, you may need membership in the **`disk`** group and **`pes3-disc-dump-linux`** (included in the release tarball).

---

## How it works (simple)

1. PES3-Disc detects a PS3 layout on your disc or mount.
2. For **DIY** discs in **Smart hybrid** (default), it builds a small **disc-assisted session** on SSD (boot files + links) and reads bulk data from the disc while you play.
3. For **retail** discs, it **decrypts once** into your **PES3 library** (default Smart hybrid) for instant replay — no manual dumps or ROM downloads.
4. It starts **RPCS3** with the prepared `EBOOT.BIN`. Session files are removed when RPCS3 exits or you eject the disc.

**Storage modes** in Settings: smart hybrid (default), full library, ephemeral session, or disc-direct (DIY, zero local copy). See [docs/PES3-LIBRARY.md](docs/PES3-LIBRARY.md).

Data lives under **`RPCS3\PES3\`** (temp overlays, optional library, logs). Legacy `cache` folders migrate to `library/titles` when using persistent library.

---

## RPCS3 settings worth checking

In RPCS3:

- Enable **Automatically start games after boot** so the game runs when PES3-Disc opens the emulator.
- If the RPCS3 window closes but the game keeps running, turn off **Exit RPCS3 when process finishes** under **Settings → Emulator**.

---

## Settings in PES3-Disc (overview)

| Setting | What it does |
|---------|----------------|
| **RPCS3 path** | Location of `rpcs3.exe` / `rpcs3` |
| **Storage mode** | Smart hybrid, persistent library, ephemeral session, or disc-direct (DIY) — see [PES3 library](docs/PES3-LIBRARY.md) |
| **Library / cache path** | Optional override for legacy cache root; library uses `RPCS3\PES3\library` by default |
| **Backups** | Optional snapshots of game files (and saves) before cache is cleared |
| **Retail decrypt tool** | Path to `pes3-disc-dump.exe` (Windows) or `pes3-disc-dump-linux` |

Windows stores app settings in `%AppData%\PES3-Disc\config.json` unless `config.json` sits next to the executable.

---

## Troubleshooting

| Problem | Try this |
|---------|----------|
| **Disc not detected** | Open the drive in File Explorer — do you see `PS3_GAME\USRDIR\EBOOT.BIN`? If not, the burn or drive may be wrong. |
| **Retail disc not detected** | Check [compatible drives](https://rpcs3.net/quickstart#dumping_drives). Many drives cannot read PS3 BDs at all. |
| **RPCS3 opens but no game** | Confirm `EBOOT.BIN` exists and is decrypted (DIY). Test manually in RPCS3: **File → Boot Game** and pick `EBOOT.BIN`. |
| **Decrypt takes forever** | Normal for large games on a slow drive. Use an SSD for cache path; keep persistent cache to avoid repeating. |
| **Cache deleted too early** | Fully exit RPCS3 (check Task Manager for stray `rpcs3.exe`). |
| **Linux: permission denied on drive** | Add your user to the `disk` group and re-login; see [docs/LINUX.md](docs/LINUX.md). |

Logs: **`RPCS3\PES3\logs\disc-run.log`** (Windows scripts) or the path shown in app Settings.

---

## Bug reports

Use **Report bug** in the app to send feedback (title, description, and basic system info — not disc contents). See [PRIVACY.md](PRIVACY.md).

---

## Legal

- Use only discs **you own**.
- Do **not** share decrypted files or use PES3-Disc to pirate games.
- You must accept the in-app legal terms before play or decrypt.

Read more: [LEGAL.md](LEGAL.md) · [User legal guide](docs/USER-LEGAL-GUIDE.md) · [Privacy](PRIVACY.md)

---

## More documentation

| Topic | Link |
|-------|------|
| Desktop app details | [docs/GUI-APP.md](docs/GUI-APP.md) |
| PES3 library & storage tiers | [docs/PES3-LIBRARY.md](docs/PES3-LIBRARY.md) |
| DIY vs retail discs | [docs/DISC-COMPATIBILITY.md](docs/DISC-COMPATIBILITY.md) |
| Retail decrypt | [docs/RETAIL-DECRYPT.md](docs/RETAIL-DECRYPT.md) |
| Linux install | [docs/LINUX.md](docs/LINUX.md) |
| Backups | [docs/BACKUPS.md](docs/BACKUPS.md) |

---

## License

PES3-Disc is released under the [MIT License](LICENSE). Third-party components: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
