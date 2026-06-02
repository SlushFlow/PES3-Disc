# PES3-Disc desktop app (GUI)

The recommended way to use PES3-Disc is **`PES3-Disc.exe`** — one Windows app for setup, retail decrypt, and launching RPCS3.

## Build

Requirements:

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or newer (builds the GUI)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) optional (builds `pes3-disc-dump.exe` for retail discs)
- Git (to fetch `ps3-disc-dumper` when building retail decrypt)
- WPF workload: `dotnet workload install microsoft-net-sdk-wpf` (if publish complains about Windows desktop)

```powershell
powershell -ExecutionPolicy Bypass -File Build-App.ps1
```

Output: **`dist\PES3-Disc.exe`** (self-contained, 64-bit Windows).

## First run

1. Run **`PES3-Disc.exe`** (or `Start-PES3-Disc.bat` after building — it prefers `dist\PES3-Disc.exe`).
2. **Setup wizard** — pick `rpcs3.exe`, optional startup and backup options.
3. Insert a PS3 disc — the home screen lists drives and shows **Play** or **Decrypt & play**.

## Features

| Screen | What it does |
|--------|----------------|
| **Setup** | RPCS3 path, delete cache after play, backups, run at login |
| **Home** | Auto-scan optical drives; play DIY discs; decrypt retail discs |
| **Decrypt** | Progress UI while the PS3 Disc Dumper engine decrypts |
| **Settings** | Change RPCS3 path, scan delay, retail decrypt, backups |

Config is stored in `%AppData%\PES3-Disc\config.json` (or `config.json` next to the exe if present).  
PES3 data (cache, logs, backups) still lives under **`RPCS3\PES3\`**.

## PowerShell scripts (legacy)

The original background watcher remains available:

- `DiscRun.ps1` / `Start-PES3-Disc.bat` (when no `dist\PES3-Disc.exe`)
- `Setup.ps1`, `Backup.ps1`, `Test-PES3-Integration.ps1`

The GUI uses the same detection rules and PES3 folder layout as the scripts.

## Retail discs

Same requirements as before: [compatible Blu-ray drive](https://rpcs3.net/quickstart#dumping_drives), IRD keys online, and enough disk space. The GUI runs `pes3-disc-dump.exe` from the same `dist\` folder (built automatically when .NET 10 SDK is installed).
