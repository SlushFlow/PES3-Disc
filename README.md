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

> **Note:** Many retail PS3 Blu-ray discs are **not** exposed as normal files on a PC drive. PES3-Disc works when Windows can read `PS3_GAME` on the disc (common for self-burned copies or discs created from a rip). For encrypted retail discs, rip to folder/ISO and burn or mount with that structure.

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

## Configuration (`config.json`)

| Field | Description |
|--------|-------------|
| `Rpcs3Path` | Full path to `rpcs3.exe` |
| `ScanDelaySeconds` | Wait after insert before scanning (default `3`) |
| `UseNoGui` | If `true`, passes `--no-gui` to RPCS3 |

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
