# Disc-assisted play (storage tiers)

PES3-Disc keeps the same flow — detect disc → prepare for RPCS3 → launch — but stores game data in **tiers** so you get more benefit than always copying full trees onto the SSD.

## Storage modes (Settings)

| Mode | DIY decrypted disc | Retail disc |
|------|-------------------|-------------|
| **Smart hybrid** (default) | Small **disc-assisted overlay** on SSD; bulk files read from the disc via links | Decrypt into a **temp session**; removed when RPCS3 exits or disc ejects |
| **Persistent library** | Full copy into `library/titles/` | Full decrypt into library; instant replay |
| **Ephemeral session** | Full copy to `temp/session-*`, deleted when RPCS3 exits | Same |
| **Disc direct** | Boot `EBOOT.BIN` on the disc/mount (zero local copy) | Still requires decrypt (session or library) |

Retail **encrypted** discs always need a decrypt step somewhere RPCS3 can read decrypted files. Smart hybrid avoids keeping that decrypt as a permanent “dump library” unless you choose **Persistent library**.

## Disc-assisted overlay (Smart hybrid DIY)

1. PES3-Disc creates `PES3/temp/disc-overlay-{id}/` with the same `PS3_GAME` layout.
2. **Copied locally:** `EBOOT.BIN`, `PARAM.SFO`, icons, and other small/critical files (up to `OverlayMaxLocalMegabytes`).
3. **Linked from disc:** larger assets (symlinks on Linux; symlinks or junctions on Windows).
4. RPCS3 launches from the overlay `EBOOT.BIN` while the disc stays inserted.
5. When RPCS3 exits **or** the disc is ejected, only the overlay folder is deleted.

## On disk layout

```
RPCS3/PES3/
  library/
    titles/<TITLE_ID or product code>/PS3_GAME/...   ← persistent library only
    .pes3-library-index.json
  cache/          ← legacy flat folders (migrated to library/titles)
  temp/
    disc-overlay-*   ← smart hybrid DIY sessions
    session-*        ← ephemeral retail/DIY sessions
  state/
    active-sessions.json   ← volumes with cleanup pending (eject handling)
```

## Config

`config.json`:

```json
"StorageMode": "SmartHybrid",
"CleanupSessionsOnDiscEject": true,
"OverlayMaxLocalMegabytes": 2048,
"DeleteCacheAfterPlay": false
```

`DeleteCacheAfterPlay` remains for older configs: `true` implies **Ephemeral session** when `StorageMode` is empty.

## Platform notes

- **Windows** (app + `Ps3DiscRun.ps1`): overlay + eject cleanup.
- **Linux** (CLI + Avalonia): same core behavior; symlinks for disc-backed files.

## Migration

Existing `PES3/cache/<title>` folders move to `PES3/library/titles/<title>` on first run after upgrade (persistent library entries only).
