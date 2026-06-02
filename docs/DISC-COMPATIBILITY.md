# Disc compatibility (official vs DIY)

PES3-Disc only works when **Windows can read files** on the inserted volume. That is the main split between retail PS3 discs and DIY burns.

## Summary

| Disc type | Works with PES3-Disc? | Works on a stock PS3? | Notes |
|-----------|------------------------|------------------------|--------|
| **DIY burn** (UDF/ISO9660 with `PS3_GAME` + `PS3_DISC.SFB`) | **Yes** | **No** (stock console) | PC-readable layout; RPCS3 needs **decrypted** `EBOOT.BIN`. |
| **Decrypted dump burned/mounted** (folder from PS3 Disc Dumper, rip, etc.) | **Yes** | Varies | Same as DIY if the burn/mount exposes `PS3_GAME\USRDIR\EBOOT.BIN`. |
| **Retail / official PS3 Blu-ray** | **Yes, with decrypt** | **Yes** (on PS3) | Use PES3-Disc retail decrypt (`Setup.ps1 -RetailDecrypt`) — same requirements as PS3 Disc Dumper. |
| **Retail disc, incompatible drive** | **No** | Yes on console | Drive cannot read PS3 media; upgrade to a [listed BD drive](https://rpcs3.net/quickstart#dumping_drives). |

PES3-Disc decrypts retail discs (or copies DIY discs) into the shared **`RPCS3\PES3\cache`** folder, then launches RPCS3 on the cached `EBOOT.BIN`. It does not stream decryption inside RPCS3 ([RPCS3 #18345](https://github.com/RPCS3/rpcs3/issues/18345)).

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

## Official retail PS3 discs (decrypt in PES3-Disc)

With a [compatible Blu-ray drive](https://rpcs3.net/quickstart#dumping_drives), Windows may expose `PS3_DISC.SFB` and/or encrypted `EBOOT.BIN`. PES3-Disc decrypts to the same **`RPCS3\PES3\cache`** folder used for DIY discs, then launches RPCS3 from SSD.

On incompatible drives:

- The BD drive reports **no files** or only non-game content.
- There is **no** `PS3_GAME` folder on the drive letter.
- PES3-Disc cannot decrypt in-place (upgrade the drive or dump elsewhere first).

**Alternative workflow:** dump with **PS3 Disc Dumper** → burn/mount as a DIY disc, or point RPCS3 at the dump folder directly.

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
