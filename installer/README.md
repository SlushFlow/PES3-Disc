# PES3-Disc installer

## For end users

Download **`PES3-Disc-Setup.exe`** from [Releases](https://github.com/SlushFlow/PES3-Disc/releases) (after you publish it), or build locally.

The installer:

1. Downloads and installs **.NET 8 Desktop Runtime** (x64)
2. Downloads and installs **.NET 10 Desktop Runtime** (x64)
3. Copies **PES3-Disc** and **pes3-disc-dump.exe** to `C:\Program Files\PES3-Disc`
4. Creates a Start menu shortcut (optional desktop / startup icons)

Requires **Administrator** and an internet connection during setup.

## Build the setup EXE

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download) and [Inno Setup 6](https://jrsoftware.org/isdl.php)
2. From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File Build-Installer.ps1
```

Output: `installer\output\PES3-Disc-Setup.exe`

## Without Inno Setup (PowerShell only)

After `Build-App.ps1`, run **as Administrator**:

```powershell
powershell -ExecutionPolicy Bypass -File installer\Install-PES3-Disc.ps1
```

Same runtime installs + copy to Program Files (no single setup.exe).
