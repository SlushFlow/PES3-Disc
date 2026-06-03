# Third-party notices

PES3-Disc includes or depends on the following third-party software. This file is provided for distribution compliance. See also [LEGAL.md](LEGAL.md).

## Bundled with releases

### PS3 Disc Dumper engine (MIT)

Retail disc decryption uses code from [13xforever/ps3-disc-dumper](https://github.com/13xforever/ps3-disc-dumper), built as `pes3-disc-dump.exe` (Windows) and `pes3-disc-dump-linux` (Linux). License: MIT (see upstream repository).

### .NET runtime

Published Windows and Linux builds are self-contained and include the [.NET runtime](https://github.com/dotnet/runtime) under the MIT license. The Windows installer may also download Microsoft .NET Desktop Runtimes separately.

### Avalonia UI (Linux GUI)

The Linux graphical app uses [Avalonia](https://github.com/AvaloniaUI/Avalonia) (MIT) and [Avalonia.Fonts.Inter](https://github.com/AvaloniaUI/Avalonia.Fonts.Inter) (OFL-1.1 for Inter font).

## Build-time only (not shipped in app binaries unless noted)

- [Inno Setup](https://jrsoftware.org/isinfo.php) — used to compile `PES3-Disc-Setup.exe` (see Inno Setup license on their website).
- [Chocolatey](https://chocolatey.org/) — optional CI install of Inno Setup.

## Not bundled

- **RPCS3** — You must install RPCS3 yourself. RPCS3 is licensed under GPL-2.0; PES3-Disc does not distribute RPCS3.
- **IRD / key files** — Obtained at runtime from public sources used by PS3 Disc Dumper; you are responsible for lawful use.

## Bug report API (optional)

If enabled, the app may contact a hosted API (default: Render). That service uses ASP.NET Core and SQLite (see Microsoft licenses). See [PRIVACY.md](PRIVACY.md).
