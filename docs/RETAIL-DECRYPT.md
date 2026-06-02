# Retail (official) disc decryption

PES3-Disc can decrypt **official PS3 Blu-ray games** using the same engine as [PS3 Disc Dumper](https://github.com/13xforever/ps3-disc-dumper) (by 13xforever), wrapped as `pes3-disc-dump.exe`.

## One-time setup

1. Install **[.NET 10 SDK](https://dotnet.microsoft.com/download)**.
2. Run:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Setup.ps1 -RetailDecrypt
   ```
3. This clones `external/ps3-disc-dumper` and builds `tools\pes3-disc-dump.exe`.

Or set `"DumpCliPath"` in `config.json` if you built the CLI elsewhere.

## Requirements (same as PS3 Disc Dumper)

- **Compatible Blu-ray drive** (typically MediaTek chipset — [RPCS3 drive list](https://rpcs3.net/quickstart#dumping_drives))
- Windows must see **`PS3_DISC.SFB`** on the disc letter (many official discs expose this even when game files are encrypted)
- A matching **IRD / Redump decryption key** for your game (downloaded automatically when possible)
- **Free disk space** for the full decrypted game (often 10–40+ GB)
- **Time**: often 30–90+ minutes per disc

## PES3 folder (next to RPCS3)

When `Rpcs3Path` is set, PES3-Disc stores data under **`RPCS3\PES3\`** (not mixed with `dev_hdd0`):

```
RPCS3\
  rpcs3.exe
  dev_hdd0\          ← RPCS3 saves, installs (never deleted by PES3-Disc)
  PES3\
    cache\           ← persistent dumps (only if DeleteCacheAfterPlay is false)
    temp\            ← ephemeral decrypt sessions (default)
    logs\disc-run.log
    state\prompted-volumes.json
```

## How it works (default: ephemeral cache)

1. You insert an official disc → PES3-Disc detects **EncryptedRetail**.
2. You confirm **Decrypt and play**.
3. `pes3-disc-dump.exe` decrypts into **`PES3\temp\session-…`** (or `PES3\cache` if you keep cache).
4. RPCS3 launches with the decrypted `EBOOT.BIN`.
5. When you **close RPCS3**, PES3-Disc deletes the decrypt folder automatically.
6. **Save data** stays in `dev_hdd0` — only the ripped game files are removed.

Set `"DeleteCacheAfterPlay": false` to keep decrypted games in `PES3\cache` for faster re-plays.

## Config (`config.json`)

| Field | Default | Meaning |
|--------|---------|---------|
| `EnableRetailDecrypt` | `true` | Offer decrypt for detected retail layouts |
| `DecryptUnknownOpticalMedia` | `false` | Also offer decrypt when **no** PS3 files are visible (empty-looking disc) |
| `DeleteCacheAfterPlay` | `true` | Remove decrypt folder when RPCS3 exits |
| `DumpCachePath` | `""` (empty) | Use `RPCS3\PES3\cache`; set only to override |
| `DumpCliPath` | `""` | Override path to `pes3-disc-dump.exe` |

## Limits

- **Not a new crack** — uses the same keys and drive requirements as PS3 Disc Dumper.
- **Completely empty drives** (no `PS3_DISC.SFB` in Explorer) cannot be decrypted; the drive/firmware cannot read the disc as PS3 media.
- **RPCS3** still needs normal firmware/settings; decryption only produces a playable folder.
- You must **own the disc** you dump; respect local laws and the RPCS3 / tool licenses.

## Troubleshooting

| Error | What to do |
|--------|------------|
| `No PS3 disc found` | Drive not compatible, or disc not mounted with `PS3_DISC.SFB` |
| `No valid disc decryption key` | Game not in IRD/Redump DB; add IRD to `%LocalAppData%\ps3-disc-dumper\ird` |
| `Direct disk access denied` | Run as administrator or close apps using the drive |
| Build fails | Install .NET 10 SDK; run `git submodule update --init` |

Manual test:

```powershell
tools\pes3-disc-dump.exe --output C:\Temp\ps3dump --drive E
```
