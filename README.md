# PES3-Disc

**P**lay**S**tation **E**mulation **S**tation **3** **Disc** — prompt to run PS3 discs in [RPCS3](https://rpcs3.net/) when you insert a disc.

Repository: [github.com/SlushFlow/PES3-Disc](https://github.com/SlushFlow/PES3-Disc)

When you insert a PS3 game disc (or a burned disc with the standard PS3 folder layout), PES3-Disc helps you **decrypt** (retail discs) and **play** in **RPCS3**.

## Desktop app (recommended)

### Download (recommended)

**[GitHub Releases](https://github.com/SlushFlow/PES3-Disc/releases)**

| Platform | Artifact |
|----------|----------|
| **Windows** | `PES3-Disc-Setup.exe` (installer) + portable ZIP |
| **Linux** | `PES3-Disc-linux-x64.tar.gz` — **Avalonia GUI** + `pes3-disc-dump-linux` ([guide](docs/LINUX.md)) |

The installer sets up the app plus **.NET 8** and **.NET 10** Desktop Runtimes (internet required during install).

A portable ZIP (`PES3-Disc-portable-win-x64.zip`) is also attached if you already have the runtimes.

### Publish a new release (maintainers)

Push a version tag; GitHub Actions builds and uploads the release automatically:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Or run the **Release** workflow manually under Actions:

- **Artifacts only** — leave *Create a GitHub Release* unchecked (default).
- **Publish a release** — check *Create a GitHub Release* and set *Release tag* to a semver tag like `v1.0.1` (must start with `v` and a version number). The workflow creates the tag on that commit and uploads assets.

### Build installer locally

```powershell
powershell -ExecutionPolicy Bypass -File Build-Installer.ps1
```

Details: [installer/README.md](installer/README.md)

### Run without installer

```powershell
powershell -ExecutionPolicy Bypass -File Build-App.ps1
```

Run **`dist\PES3-Disc.exe`**. First launch opens a setup wizard (RPCS3 path, options). The main window scans drives and offers **Play** or **Decrypt & play**.

Details: [docs/GUI-APP.md](docs/GUI-APP.md)

### Bug reports

Use **Report bug** in the app to send feedback. The API and Render deploy config (`render.yaml`, `Dockerfile`) are in this repo — see [docs/BUG-REPORTS-API.md](docs/BUG-REPORTS-API.md).

### Legacy background watcher

Without the exe, use PowerShell: copy `config.example.json` → `config.json`, then **`Start-PES3-Disc.bat`** (hidden watcher + tray-style prompts).

## Requirements

- **Windows 10/11** or **Linux x64** (CLI)
- **RPCS3** installed (`rpcs3.exe` / `rpcs3`)
- Disc readable in Windows with this layout (typical for burned / file-based discs):

  ```
  X:\PS3_GAME\USRDIR\EBOOT.BIN
  X:\PS3_GAME\PARAM.SFO
  ```

  Optional root file: `PS3_DISC.SFB`

## Official vs DIY discs

| Disc type | PES3-Disc |
|-----------|-----------|
| **DIY / burned / mounted** disc with decrypted `EBOOT.BIN` | **Works** — copies to **PES3 cache** (SSD) then launches RPCS3 |
| **Retail / official PS3 Blu-ray** | **Decrypt then play** — same **PES3 cache**; reuse cached copy on next insert |

For official discs, run **one-time** setup:

```powershell
powershell -ExecutionPolicy Bypass -File Setup.ps1 -RetailDecrypt
```

Then insert the disc → **Decrypt & play** or **Play from cache** if already decrypted. DIY and retail both use **`RPCS3\PES3\cache`** so RPCS3 reads from SSD instead of the optical drive. Cache is **removed when you close RPCS3** by default (toggle in Settings); saves in `dev_hdd0` are kept. Requires a [compatible Blu-ray drive](https://rpcs3.net/quickstart#dumping_drives) and IRD keys for retail.

- DIY / layout details: [docs/DISC-COMPATIBILITY.md](docs/DISC-COMPATIBILITY.md)  
- Retail decrypt: [docs/RETAIL-DECRYPT.md](docs/RETAIL-DECRYPT.md)

Run layout tests anytime:

```powershell
powershell -ExecutionPolicy Bypass -File Test-Ps3DiscDetection.ps1
```

### Test disc fixtures (no real game data)

Simulated DIY and retail layouts under `test-fixtures/`:

```powershell
powershell -ExecutionPolicy Bypass -File test-fixtures\Build-TestFixtures.ps1
powershell -ExecutionPolicy Bypass -File Test-PES3-Integration.ps1        # ~10+ min
powershell -ExecutionPolicy Bypass -File Test-PES3-Integration.ps1 -Quick  # ~2 min smoke test
```

Scan fixtures without a burner drive:

```powershell
powershell -ExecutionPolicy Bypass -File DiscRun.ps1 -Scan -NonInteractive `
  -TestVolume ".\test-fixtures\diy-demo-disc", ".\test-fixtures\retail-encrypted-disc"
```

## Quick start

1. [Download or clone](https://github.com/SlushFlow/PES3-Disc) this repo.
2. Copy `config.example.json` to `config.json` and set `Rpcs3Path`, **or** run:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Setup.ps1 -Config
   ```
3. Double-click **`Start-PES3-Disc.bat`** (runs in the background; log: `RPCS3\PES3\logs\disc-run.log`).
4. Insert a PS3-layout disc → confirm the dialog → RPCS3 boots the game.

### Run at Windows login

```powershell
powershell -ExecutionPolicy Bypass -File Setup.ps1 -Startup
```

Or run everything: `Setup.ps1 -All`

This adds a **PES3-Disc** shortcut to your Startup folder. Remove it from **Settings → Apps → Startup** to disable.

## PES3 folder layout

With `Rpcs3Path` configured, runtime data lives under **`RPCS3\PES3\`**:

| Path | Purpose |
|------|---------|
| `PES3\temp\` | Ephemeral decrypt (default; deleted when RPCS3 closes) |
| `PES3\cache\` | Persistent decrypts if `DeleteCacheAfterPlay` is `false` |
| `PES3\logs\` | `disc-run.log` |
| `PES3\state\` | Watcher state (not game saves) |
| `PES3\backups\` | Game (+ optional save) snapshots before delete/replace |

`config.json` stays in the PES3-Disc install folder. **`dev_hdd0` is never touched** by cache cleanup (save folders may be **copied** into backups when enabled).

### Backups

Before ephemeral decrypt folders are removed, PES3-Disc can copy the game tree (and optionally `dev_hdd0\savedata\<TITLE_ID>`) under `PES3\backups\`. List or restore with `Backup.ps1`; details in [docs/BACKUPS.md](docs/BACKUPS.md).

## Configuration (`config.json`)

| Field | Description |
|--------|-------------|
| `Rpcs3Path` | Full path to `rpcs3.exe` |
| `ScanDelaySeconds` | Wait after insert before scanning (default `3`) |
| `UseNoGui` | If `true`, passes `--no-gui` to RPCS3 |
| `DeleteCacheAfterPlay` | If `true` (default), delete decrypt folder when RPCS3 exits |
| `DumpCachePath` | Leave `""` to use `RPCS3\PES3\cache` |
| `EnableBackups` | Snapshot game files before cache delete/replace (default `true`) |
| `BackupSaves` | Include RPCS3 save data for that title in each backup (default `true`) |
| `BackupOnLaunch` | Backup on every launch, not only ephemeral decrypt (default `false`) |
| `MaxBackupsPerTitle` | Keep newest N snapshots per title (default `3`) |
| `BackupPath` | Custom backup root; `""` = `RPCS3\PES3\backups` |

Example:

```json
{
  "Rpcs3Path": "D:\\Emulators\\RPCS3\\rpcs3.exe",
  "ScanDelaySeconds": 3,
  "UseNoGui": false
}
```

## Files (project root)

| File | Purpose |
|------|---------|
| `Build-Installer.ps1` | Build **`installer\output\PES3-Disc-Setup.exe`** (app + .NET 8/10) |
| `Build-App.ps1` | Build **`dist\PES3-Disc.exe`** (GUI only) |
| `installer\` | Inno Setup script + PowerShell installer |
| `src\PES3-Disc.App\` | WPF desktop app |
| `src\PES3-Disc.Core\` | Shared C# library (detect, decrypt, launch) |
| `Start-PES3-Disc.bat` | Launch GUI exe if built, else PowerShell watcher |
| `DiscRun.ps1` | Legacy watcher + `-Scan` / `-RemoveOnly` |
| `Ps3DiscRun.ps1` | Legacy PowerShell library |
| `Setup.ps1` | Legacy `-Config`, `-Startup`, `-RetailDecrypt` |
| `Backup.ps1` | `-List`, `-Restore`, `-BackupNow` |
| `Test-PES3-Integration.ps1` | Full integration test |
| `config.example.json` | Config template (scripts / portable exe) |
| `docs/GUI-APP.md` | Desktop app guide |
| `docs/` | Compatibility and retail decrypt guides |

## RPCS3 settings

- Enable **Automatically start games after boot** (default) so the game runs when RPCS3 opens with `EBOOT.BIN`.
- If the emulator window closes but the game keeps running, disable **Exit RPCS3 when process finishes** under **Settings → Emulator**.

## Troubleshooting

- **No prompt:** Ensure `Start-PES3-Disc.bat` is running; open `RPCS3\PES3\logs\disc-run.log`. Increase `ScanDelaySeconds` if the drive is slow to mount (insert events also wait this long before scanning).
- **Prompt again after eject/re-insert:** Normal; prompt state resets when the volume is ejected.
- **Temp decrypt deleted too early:** RPCS3 must fully exit; cleanup waits for the launched process plus a short grace period. Close stray `rpcs3.exe` instances if needed.
- **Duplicate prompts on insert:** Overlapping scans are suppressed for ~12 seconds; if issues persist, restart the watcher.
- **RPCS3 opens but game does not start:** Verify `EBOOT.BIN` exists on the disc; test manually:  
  `"C:\path\to\rpcs3.exe" "X:\PS3_GAME\USRDIR\EBOOT.BIN"`

## Legal and privacy

PES3-Disc is licensed under the [MIT License](LICENSE). See also:

- [LEGAL.md](LEGAL.md) — lawful use, prohibited uses, persistent cache, disclaimer
- [docs/USER-LEGAL-GUIDE.md](docs/USER-LEGAL-GUIDE.md) — short checklist for end users
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) — ps3-disc-dumper, .NET, Avalonia, etc.
- [PRIVACY.md](PRIVACY.md) — optional bug report API; local cache stays on your PC
- [SECURITY.md](SECURITY.md) — reporting vulnerabilities

The app asks you to confirm you own the disc, will not redistribute files, and comply with local law before decrypt or copy. You must own discs you decrypt. PES3-Disc is not affiliated with Sony, PlayStation, or RPCS3.

## Performance tips

- Use a **fast SSD** for `DumpCachePath` / `RPCS3\PES3\cache` (Settings).
- Turn off **Delete cache after play** to avoid re-decrypting the same disc.
- Use a **compatible Blu-ray drive** ([RPCS3 quickstart](https://rpcs3.net/quickstart#dumping_drives)); dump speed is limited by the drive and disc, not the GUI.
- DIY discs are staged with multi-threaded **robocopy** (Windows) or **rsync** (Linux) when available.
