# PES3-Disc backups

PES3-Disc can snapshot game files (and optionally RPCS3 save data) before they are deleted or replaced, so you can recover from a bad decrypt, accidental cache wipe, or corruption.

## Where backups live

By default:

```
RPCS3\PES3\backups\<TITLE_ID>\<yyyyMMdd-HHmmss>\
  manifest.json
  game\          (copy of the game root / cache folder)
  savedata\      (optional, from dev_hdd0\savedata\<TITLE_ID>)
```

Set `BackupPath` in `config.json` to use a custom folder.

## When backups run automatically

| Event | Backup |
|--------|--------|
| You click **Yes** to play (ephemeral decrypt / temp session) | **before_play** — once before RPCS3 starts |
| You click **No** (decrypt discarded) | **declined_or_cleanup** |
| Persistent cache replaced on re-decrypt | **before_cache_replace** — old cache folder only |
| RPCS3 exits and temp cache is deleted | No second backup (already done at **before_play**) |

Within two minutes, the same title and reason will not create a duplicate snapshot (for example if multiple cleanup paths run close together).

`BackupOnLaunch`: set `true` to also backup DIY / disc games that are not using ephemeral cleanup (off by default).

## Configuration

| Field | Default | Description |
|--------|---------|-------------|
| `EnableBackups` | `true` | Master switch |
| `BackupSaves` | `true` | Include `dev_hdd0\savedata\<TITLE_ID>` when present |
| `BackupOnLaunch` | `false` | Backup every launch, not only ephemeral sessions |
| `MaxBackupsPerTitle` | `3` | Oldest snapshots pruned per title |
| `BackupPath` | `""` | Custom root; empty = `RPCS3\PES3\backups` |

## List and restore

List snapshots:

```powershell
powershell -ExecutionPolicy Bypass -File Backup.ps1 -List
```

Restore a snapshot (folder name or full path):

```powershell
powershell -ExecutionPolicy Bypass -File Backup.ps1 -Restore "20260602-143022" -TitleId BLUS12345
powershell -ExecutionPolicy Bypass -File Backup.ps1 -Restore "C:\...\backups\BLUS12345\20260602-143022" -RestoreSaves
```

Manual backup of any game folder or EBOOT:

```powershell
powershell -ExecutionPolicy Bypass -File Backup.ps1 -BackupNow "D:\Games\MyGame\PS3_GAME\USRDIR\EBOOT.BIN"
```

Restored games go to `PES3\cache\<TITLE_ID>` unless you pass `-RestoreTo`. Existing targets are renamed with a `.before-restore.<timestamp>` suffix first.

## What is not backed up

- RPCS3 `dev_hdd0` outside `savedata\<TITLE_ID>` (unless you copy it yourself)
- Ephemeral temp folders that never contained a complete game tree
- Discs you never decrypted or launched

See also: [RETAIL-DECRYPT.md](RETAIL-DECRYPT.md), [DISC-COMPATIBILITY.md](DISC-COMPATIBILITY.md).
