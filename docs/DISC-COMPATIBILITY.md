# Disc compatibility (official vs DIY)

PES3-Disc only works when **Windows can read files** on the inserted volume. That is the main split between retail PS3 discs and DIY burns.

## Summary

| Disc type | Works with PES3-Disc? | Works on a stock PS3? | Notes |
|-----------|------------------------|------------------------|--------|
| **DIY burn** (UDF/ISO9660 with `PS3_GAME` + `PS3_DISC.SFB`) | **Yes** | **No** (stock console) | PC-readable layout; RPCS3 needs **decrypted** `EBOOT.BIN`. |
| **Decrypted dump burned/mounted** (folder from PS3 Disc Dumper, rip, etc.) | **Yes** | Varies | Same as DIY if the burn/mount exposes `PS3_GAME\USRDIR\EBOOT.BIN`. |
| **Retail / official PS3 Blu-ray** in a normal PC drive | **No** | **Yes** (on PS3) | Encrypted/proprietary format; drive often looks **empty** to Windows. |
| **Retail disc** in a **MediaTek** BD drive + manual dump | **After dump** | Yes on console | Use [PS3 Disc Dumper](https://git.rpcs3.net/rpcs3/ps3-disc-dumper), then burn or run from the **folder**—not from the raw disc. |

RPCS3 also **cannot** decrypt a live retail disc in the drive ([feature request #18345](https://github.com/RPCS3/rpcs3/issues/18345)); PES3-Disc only launches a path to `EBOOT.BIN` that already exists on a drive letter.

## DIY / file-based discs (supported)

These are what PES3-Disc is built for. Windows must see something like:

```
X:\
├── PS3_DISC.SFB          (recommended for DIY burns)
├── PS3_GAME\
│   ├── PARAM.SFO
│   └── USRDIR\
│       └── EBOOT.BIN     (must be decrypted for RPCS3)
└── PS3_UPDATE\           (optional)
```

Also supported:

- `X:\Some Game Name\PS3_GAME\USRDIR\EBOOT.BIN` (game in a subfolder)
- `X:\dev_bdvd\PS3_GAME\USRDIR\EBOOT.BIN` (console-style path from dumps)

**Burning tips for PC use**

- Use a **data** session (UDF or ISO9660), not a “video BD” layout only.
- Include **decrypted** files from a proper dump; a raw encrypted rip will be detected only if `EBOOT.BIN` is present and still may fail in RPCS3.
- Blu-ray (BD-R/BD-RE) is best for large games; DVD-R only fits smaller titles.

## Official retail PS3 discs (not supported in-drive)

On most PCs:

- The BD drive reports **no files** or only non-game content.
- There is **no** `PS3_GAME` folder on the drive letter.
- PES3-Disc will **not** show a launch prompt (nothing to detect).

This is expected: retail discs use protection and layout that standard PC Blu-ray stacks do not mount as a normal folder tree ([Super User discussion](https://superuser.com/questions/715574/how-to-mount-ps3-blu-ray-discs-on-windows-7-or-any-os-blu-ray-drive)).

**Workflow that does work:** own the disc → dump with a compatible drive and **PS3 Disc Dumper** → run from the dumped folder in RPCS3 (or burn that folder to a DIY disc for PES3-Disc).

## How to verify on your machine

1. Run automated layout tests:
   ```powershell
   powershell -ExecutionPolicy Bypass -File Test-Ps3DiscDetection.ps1
   ```
2. Insert your disc and check `disc-run.log` after `Start-PES3-Disc.bat` is running.
3. In Explorer, open the disc drive letter:
   - If you see `PS3_GAME` → PES3-Disc can prompt.
   - If the drive is empty or has no `PS3_GAME` → retail/encrypted; dump first.

## RPCS3 reminder

Even when PES3-Disc finds `EBOOT.BIN`, RPCS3 must be able to boot it (decrypted rip, correct firmware/libraries). See the [RPCS3 quickstart](https://rpcs3.net/quickstart) and [dumping guide](https://wiki.rpcs3.net/index.php?title=Help:Dumping_PlayStation_3_games).
