# PES3-Disc

**P**lay**S**tation **E**mulation **S**tation **3** **Disc** — prompt to run PS3 discs in [RPCS3](https://rpcs3.net/) when you insert a disc.

Repository: [github.com/SlushFlow/PES3-Disc](https://github.com/SlushFlow/PES3-Disc)

When you insert a PS3 game disc (or a burned disc with the standard PS3 folder layout), a dialog asks whether to launch the game in **RPCS3**. **Yes** starts RPCS3 with that disc’s `EBOOT.BIN`. **No** does nothing.

## Requirements

- **Windows 10/11**
- **RPCS3** installed (`rpcs3.exe`)
- Disc readable in Windows with this layout (typical for burned / file-based discs):

  ```
  X:\PS3_GAME\USRDIR\EBOOT.BIN
  X:\PS3_GAME\PARAM.SFO
  ```

  Optional root file: `PS3_DISC.SFB`

## Official vs DIY discs

| Disc type | PES3-Disc |
|-----------|-----------|
| **DIY / burned / mounted** disc with decrypted `EBOOT.BIN` | **Works** — prompt and launch RPCS3 |
| **Retail / official PS3 Blu-ray** | **Decrypt then play** — built-in retail decryptor (`pes3-disc-dump`) |

For official discs, run **one-time** setup:

```powershell
powershell -ExecutionPolicy Bypass -File Setup.ps1 -RetailDecrypt
```

Then insert the disc → confirm **Decrypt and play**. Decrypted files go under **`RPCS3\PES3\`** and are **removed when you close RPCS3** by default (saves in `dev_hdd0` are kept). Requires a [compatible Blu-ray drive](https://rpcs3.net/quickstart#dumping_drives) and IRD keys.

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
| `Start-PES3-Disc.bat` | Start the background watcher |
| `DiscRun.ps1` | Watcher + `-Scan` / `-RemoveOnly` for disc events |
| `Ps3DiscRun.ps1` | Core library (config, paths, retail decrypt) |
| `Setup.ps1` | `-Config`, `-Startup`, `-RetailDecrypt`, or `-All` |
| `Backup.ps1` | `-List`, `-Restore`, `-BackupNow` |
| `Test-Ps3DiscDetection.ps1` | Layout tests |
| `config.example.json` | Config template |
| `tools/` | `pes3-disc-dump.exe` (after `-RetailDecrypt` setup) |
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

## License

Use and modify freely; RPCS3 is a separate project with its own license.
