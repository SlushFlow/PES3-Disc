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
powershell -ExecutionPolicy Bypass -File Setup-RetailDecrypt.ps1
```

Then insert the disc → confirm **Decrypt and play**. Decrypted files go under **`RPCS3\PES3\`** and are **removed when you close RPCS3** by default (saves in `dev_hdd0` are kept). Requires a [compatible Blu-ray drive](https://rpcs3.net/quickstart#dumping_drives) and IRD keys.

- DIY / layout details: [docs/DISC-COMPATIBILITY.md](docs/DISC-COMPATIBILITY.md)  
- Retail decrypt: [docs/RETAIL-DECRYPT.md](docs/RETAIL-DECRYPT.md)

Run layout tests anytime:

```powershell
powershell -ExecutionPolicy Bypass -File Test-Ps3DiscDetection.ps1
```

## Quick start

1. [Download or clone](https://github.com/SlushFlow/PES3-Disc) this repo.
2. Copy `config.example.json` to `config.json` and set `Rpcs3Path`, **or** run:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Setup-Config.ps1
   ```
3. Double-click **`Start-PES3-Disc.bat`** (runs in the background; see `disc-run.log` if needed).
4. Insert a PS3-layout disc → confirm the dialog → RPCS3 boots the game.

### Run at Windows login

```powershell
powershell -ExecutionPolicy Bypass -File Install-Startup.ps1
```

This adds a **PES3-Disc** shortcut to your Startup folder. Remove it from **Settings → Apps → Startup** to disable.

## PES3 folder layout

With `Rpcs3Path` configured, runtime data lives under **`RPCS3\PES3\`**:

| Path | Purpose |
|------|---------|
| `PES3\temp\` | Ephemeral decrypt (default; deleted when RPCS3 closes) |
| `PES3\cache\` | Persistent decrypts if `DeleteCacheAfterPlay` is `false` |
| `PES3\logs\` | `disc-run.log` |
| `PES3\state\` | Watcher state (not game saves) |

`config.json` stays in the PES3-Disc install folder. **`dev_hdd0` is never touched** by cache cleanup.

## Configuration (`config.json`)

| Field | Description |
|--------|-------------|
| `Rpcs3Path` | Full path to `rpcs3.exe` |
| `ScanDelaySeconds` | Wait after insert before scanning (default `3`) |
| `UseNoGui` | If `true`, passes `--no-gui` to RPCS3 |
| `DeleteCacheAfterPlay` | If `true` (default), delete decrypt folder when RPCS3 exits |
| `DumpCachePath` | Leave `""` to use `RPCS3\PES3\cache` |

Example:

```json
{
  "Rpcs3Path": "D:\\Emulators\\RPCS3\\rpcs3.exe",
  "ScanDelaySeconds": 3,
  "UseNoGui": false
}
```

## Files

| File | Purpose |
|------|---------|
| `Start-PES3-Disc.bat` | Start the background watcher |
| `DiscRun.ps1` | Main watcher (WMI disc events) |
| `DiscRun-Scan.ps1` | Scan drives and show prompt |
| `Ps3DiscRun.Common.ps1` | Shared logic |
| `Setup-Config.ps1` | Pick RPCS3 path |
| `Install-Startup.ps1` | Add to Windows Startup |
| `disc-run.log` | Activity log (created at runtime, not in git) |
| `Test-Ps3DiscDetection.ps1` | Automated layout tests (DIY vs retail-style) |
| `docs/DISC-COMPATIBILITY.md` | Official vs DIY disc matrix |

## RPCS3 settings

- Enable **Automatically start games after boot** (default) so the game runs when RPCS3 opens with `EBOOT.BIN`.
- If the emulator window closes but the game keeps running, disable **Exit RPCS3 when process finishes** under **Settings → Emulator**.

## Troubleshooting

- **No prompt:** Ensure `Start-PES3-Disc.bat` is running; open `disc-run.log`. Increase `ScanDelaySeconds` if the drive is slow to mount.
- **Prompt again after eject/re-insert:** Normal; prompt state resets when the volume is ejected.
- **RPCS3 opens but game does not start:** Verify `EBOOT.BIN` exists on the disc; test manually:  
  `"C:\path\to\rpcs3.exe" "X:\PS3_GAME\USRDIR\EBOOT.BIN"`

## License

Use and modify freely; RPCS3 is a separate project with its own license.
