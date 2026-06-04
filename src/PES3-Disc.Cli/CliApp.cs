using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.Cli;

public static class CliApp
{
    private static Pes3Services _svc = null!;
    private static List<(OpticalDrive Drive, DiscVolumeStatus Status)> _lastScan = new();

    public static async Task<int> RunAsync(string[] args)
    {
        _svc = Pes3Services.Load();
        _svc.Initialize();

        if (args.Length == 0)
            return await WatchAsync(Array.Empty<string>());

        var cmd = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        return cmd switch
        {
            "help" or "-h" or "--help" => PrintHelp(),
            "scan" => ScanCommand(rest),
            "watch" => await WatchAsync(rest),
            "setup" => SetupCommand(rest),
            "config" => ConfigCommand(),
            "play" => await PlayCommand(rest),
            "decrypt" => await DecryptCommand(rest),
            _ when int.TryParse(cmd, out _) => await PlayCommand(args),
            _ => Unknown(cmd),
        };
    }

    private static int PrintHelp()
    {
        var help = string.Join('\n', new[]
        {
            "PES3-Disc — PlayStation Emulation Station 3 Disc (Linux CLI)",
            "",
            "Usage:",
            "  pes3-disc                 Watch optical / mounted volumes (default)",
            "  pes3-disc scan            List PS3 discs once",
            "  pes3-disc scan --test-volume <path>  Scan fixture folder(s) (CI / dev)",
            "  pes3-disc play <n>        Play disc index from last scan",
            "  pes3-disc decrypt <n>     Decrypt retail disc and play",
            "  --accept-legal-terms      Required once for play/decrypt (see LEGAL.md)",
            "  pes3-disc setup <path>    Set RPCS3 executable path",
            "  pes3-disc config          Show configuration",
            "  pes3-disc watch           Poll drives every N seconds",
            "",
            "Options (watch):",
            "  --interval <sec>          Scan delay (default: from config)",
            "",
            "Cache: DIY discs copy to RPCS3/PES3/cache (same as Windows). Use config.json in:",
            Pes3Config.GetDefaultConfigPath(),
        });
        Console.WriteLine(help);
        return 0;
    }

    private static int Unknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command: {cmd}. Run pes3-disc help");
        return 1;
    }

    private static int ScanCommand(string[] args)
    {
        var testRoots = ParseTestVolumeArgs(args);
        if (testRoots.Count > 0)
        {
            RunScanTestVolumes(testRoots);
            return 0;
        }

        RunScan();
        return 0;
    }

    private static List<string> ParseTestVolumeArgs(string[] args)
    {
        var roots = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--test-volume" or "-T" && i + 1 < args.Length)
                roots.Add(args[++i]);
        }

        return roots;
    }

    private static void RunScanTestVolumes(IReadOnlyList<string> roots)
    {
        _lastScan.Clear();
        var index = 0;
        foreach (var root in roots)
        {
            var normalized = Path.GetFullPath(root);
            if (!normalized.EndsWith(Path.DirectorySeparatorChar))
                normalized += Path.DirectorySeparatorChar;

            var drive = new OpticalDrive
            {
                Letter = (char)('A' + (index % 26)),
                Root = normalized,
                Id = $"TESTVOL|{normalized}",
                VolumeLabel = Path.GetFileName(normalized.TrimEnd(Path.DirectorySeparatorChar, '/')),
            };

            var status = DiscDetector.GetVolumeStatus(normalized);
            if (status.Kind == DiscVolumeKind.NoPs3Layout && !_svc.Config.DecryptUnknownOpticalMedia)
                continue;

            _lastScan.Add((drive, status));
            PrintVolume(index++, drive, status);
        }

        if (_lastScan.Count == 0)
            Console.WriteLine("No PS3 layout detected on test volumes.");
    }

    private static void RunScan()
    {
        if (_svc.Config.CleanupSessionsOnDiscEject)
            _svc.SessionRegistry.CleanupEjectedVolumes();

        _lastScan.Clear();
        var drives = DiscDetector.GetOpticalDrives();
        if (drives.Count == 0)
        {
            Console.WriteLine("No optical or mounted disc volumes found.");
            Console.WriteLine("Mount your disc under /media or /run/media, then scan again.");
            return;
        }

        var index = 0;
        foreach (var drive in drives)
        {
            var status = DiscDetector.GetVolumeStatus(drive.Root);
            if (status.Kind == DiscVolumeKind.NoPs3Layout && !_svc.Config.DecryptUnknownOpticalMedia)
                continue;

            _lastScan.Add((drive, status));
            PrintVolume(index++, drive, status);
        }

        if (_lastScan.Count == 0)
            Console.WriteLine("No PS3 layout detected on available volumes.");
    }

    private static void PrintVolume(int index, OpticalDrive drive, DiscVolumeStatus status)
    {
        var title = status.Game?.Title ?? status.Kind.ToString();
        var cached = _svc.Cache.TryGetCached(drive.Id, status.Game?.TitleId, null);
        var cacheTag = cached is not null ? " [cached]" : "";
        Console.WriteLine($"[{index}] {drive.DisplayName} — {title}{cacheTag}");
        Console.WriteLine($"     {status.Message}");
        Console.WriteLine($"     {drive.Root}");
        if (!string.IsNullOrEmpty(drive.DeviceNode))
            Console.WriteLine($"     {drive.DeviceNode}");
    }

    private static async Task<int> WatchAsync(string[] args)
    {
        var interval = _svc.Config.ScanDelaySeconds;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--interval" or "-i" && i + 1 < args.Length && int.TryParse(args[i + 1], out var sec))
                interval = Math.Max(2, sec);
        }

        Console.WriteLine($"PES3-Disc watching (every {interval}s). Ctrl+C to exit.");
        Console.WriteLine($"RPCS3: {_svc.Config.Rpcs3Path}");
        Console.WriteLine($"PES3:  {_svc.Paths.Pes3Root ?? "(set RPCS3 path)"}");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        while (!cts.Token.IsCancellationRequested)
        {
            RunScan();
            if (_lastScan.Count > 0)
            {
                for (var i = 0; i < _lastScan.Count; i++)
                {
                    var (drive, status) = _lastScan[i];
                    if (status.Kind == DiscVolumeKind.Playable && status.Game is not null)
                    {
                        Console.Write($"Play DIY disc [{i}] {status.Game.Title}? [y/N] ");
                        if (Console.ReadLine()?.Trim().ToLowerInvariant() is "y" or "yes")
                        {
                            if (EnsureLegalAcceptance(Array.Empty<string>()))
                                await PlayVolumeAsync(drive, status.Game, cts.Token);
                        }
                    }
                    else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn
                             && _svc.Config.EnableRetailDecrypt)
                    {
                        var cached = _svc.Cache.TryGetCached(drive.Id, null, null);
                        if (cached is not null)
                        {
                            Console.Write($"Play retail from cache [{i}]? [y/N] ");
                            if (Console.ReadLine()?.Trim().ToLowerInvariant() is "y" or "yes")
                                await LaunchSessionAsync(_svc.Cache.SessionFromCached(cached), cts.Token);
                        }
                        else if (_svc.Decryptor.IsAvailable)
                        {
                            Console.Write($"Decrypt retail disc [{i}]? [y/N] ");
                            if (Console.ReadLine()?.Trim().ToLowerInvariant() is "y" or "yes")
                            {
                                if (EnsureLegalAcceptance(Array.Empty<string>()))
                                    await DecryptVolumeAsync(drive, cts.Token);
                            }
                        }
                    }
                }
            }
            try { await Task.Delay(TimeSpan.FromSeconds(interval), cts.Token); }
            catch (OperationCanceledException) { break; }
            Console.WriteLine();
        }

        return 0;
    }

    private static bool EnsureLegalAcceptance(string[] args)
    {
        if (LegalTerms.IsAccepted(_svc.Config))
            return true;

        if (args.Any(a => string.Equals(a, "--accept-legal-terms", StringComparison.OrdinalIgnoreCase)))
        {
            LegalTerms.RecordAcceptance(_svc.Config);
            _svc.SaveConfig();
            Pes3Log.Write($"Legal terms accepted via flag ({LegalTerms.CurrentVersion}).");
            return true;
        }

        Console.Write(LegalTerms.CliNotice);
        Console.Write("Type I ACCEPT to continue: ");
        var line = Console.ReadLine()?.Trim();
        if (!string.Equals(line, "I ACCEPT", StringComparison.Ordinal))
        {
            Console.Error.WriteLine("Aborted. Read LEGAL.md, then run again with --accept-legal-terms if appropriate.");
            return false;
        }

        LegalTerms.RecordAcceptance(_svc.Config);
        _svc.SaveConfig();
        return true;
    }

    private static string[] StripLegalFlag(string[] args) =>
        args.Where(a => !string.Equals(a, "--accept-legal-terms", StringComparison.OrdinalIgnoreCase)).ToArray();

    private static async Task<int> PlayCommand(string[] args)
    {
        args = StripLegalFlag(args);
        if (!EnsureLegalAcceptance(args))
            return 1;
        if (_lastScan.Count == 0)
            RunScan();
        if (!TryParseIndex(args, out var index))
            return 1;
        var (_, status) = _lastScan[index];
        if (status.Game is null)
        {
            Console.Error.WriteLine("That volume has no playable DIY game.");
            return 1;
        }
        await PlayVolumeAsync(_lastScan[index].Drive, status.Game, CancellationToken.None);
        return 0;
    }

    private static async Task<int> DecryptCommand(string[] args)
    {
        args = StripLegalFlag(args);
        if (!EnsureLegalAcceptance(args))
            return 1;
        if (_lastScan.Count == 0)
            RunScan();
        if (!TryParseIndex(args, out var index))
            return 1;
        await DecryptVolumeAsync(_lastScan[index].Drive, CancellationToken.None);
        return 0;
    }

    private static bool TryParseIndex(string[] args, out int index)
    {
        index = 0;
        if (args.Length == 0 || !int.TryParse(args[0], out index) || index < 0 || index >= _lastScan.Count)
        {
            Console.Error.WriteLine("Usage: pes3-disc play|decrypt <index>  (run scan first)");
            return false;
        }
        return true;
    }

    private static async Task PlayVolumeAsync(OpticalDrive drive, DetectedGame game, CancellationToken ct)
    {
        PlaySession session;
        var cached = _svc.Cache.TryGetCached(drive.Id, game.TitleId, null);
        if (cached is not null)
        {
            Console.WriteLine($"Using cache: {cached.CacheDir}");
            session = _svc.Cache.SessionFromCached(cached);
        }
        else
        {
            Console.WriteLine("Copying disc to PES3 cache…");
            session = await _svc.Cache.PrepareDiyPlayAsync(
                drive,
                game,
                new Progress<StageProgress>(p =>
                {
                    if (p.TotalFiles > 0)
                        Console.Write($"\r  {p.FilesCopied}/{p.TotalFiles} ({p.Percent}%)   ");
                }),
                ct);
            Console.WriteLine();
        }

        await LaunchSessionAsync(session, ct);
    }

    private static async Task DecryptVolumeAsync(OpticalDrive drive, CancellationToken ct)
    {
        if (!_svc.Decryptor.IsAvailable)
        {
            Console.Error.WriteLine("pes3-disc-dump not found. Build with .NET 10 or set DumpCliPath.");
            return;
        }

        var cached = await _svc.Cache.TryGetRetailCachedAsync(
            drive,
            token => _svc.Decryptor.ProbeDiscAsync(drive, token),
            ct).ConfigureAwait(false);
        if (cached is not null)
        {
            Console.WriteLine($"Using cached decrypt: {cached.CacheDir}");
            await LaunchSessionAsync(_svc.Cache.SessionFromCached(cached), ct);
            return;
        }

        DiscProbeResult? probe = null;
        try
        {
            probe = await _svc.Decryptor.ProbeDiscAsync(drive, ct).ConfigureAwait(false);
        }
        catch
        {
            // ignore
        }

        var mode = Pes3StorageModeResolver.Resolve(_svc.Config);
        var outputDir = _svc.Cache.ResolveRetailOutputDir(probe?.ProductCode);
        Directory.CreateDirectory(outputDir);
        var cleanup = mode == Pes3StorageMode.PersistentLibrary
            ? new List<string>()
            : new List<string> { outputDir };

        Console.WriteLine($"Decrypting to {outputDir} (this may take a long time)…");
        var result = await _svc.Decryptor.DecryptAsync(drive, outputDir, new Progress<DecryptProgress>(PrintDecryptProgress), ct);
        Console.WriteLine();
        if (!result.Success)
        {
            Console.Error.WriteLine(result.ErrorMessage ?? "Decrypt failed.");
            if (mode != Pes3StorageMode.PersistentLibrary && Directory.Exists(outputDir))
            {
                try { Directory.Delete(outputDir, true); } catch { /* ignore */ }
            }
            return;
        }

        var session = _svc.Cache.FinalizeRetailDecrypt(result, outputDir, cleanup);
        session = new PlaySession
        {
            EbootPath = session.EbootPath,
            CleanupDirs = session.CleanupDirs,
            FromCache = session.FromCache,
            CacheDir = session.CacheDir,
            Tier = session.Tier,
            VolumeId = drive.Id,
            DiscRoot = drive.Root,
        };
        await LaunchSessionAsync(session, ct);
    }

    private static void PrintDecryptProgress(DecryptProgress p)
    {
        if (p.TotalFileSectors > 0)
            Console.Write($"\r  Decrypt: {p.Percent}% (file {p.CurrentFile}/{p.TotalFiles})   ");
        else if (!string.IsNullOrEmpty(p.Title))
            Console.Write($"\r  {p.Title}…   ");
    }

    private static async Task LaunchSessionAsync(PlaySession session, CancellationToken ct)
    {
        var proc = await _svc.Launcher.LaunchSessionAsync(session, ct).ConfigureAwait(false);
        if (proc is null)
        {
            Console.Error.WriteLine("Could not start RPCS3. Run: pes3-disc setup /path/to/rpcs3");
            return;
        }

        if (session.OverlayStats is { } stats)
            Console.WriteLine($"Disc-assisted: {stats.Summary}");

        Console.WriteLine($"Launched RPCS3 (PID {proc.Id}). Waiting for exit…");
        await proc.WaitForExitAsync(ct);
        Console.WriteLine("RPCS3 exited.");
    }

    private static int SetupCommand(string[] args)
    {
        string? path = args.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            path = _svc.Launcher.FindRpcs3();
            if (path is null)
            {
                Console.Error.WriteLine("Usage: pes3-disc setup /path/to/rpcs3");
                return 1;
            }
            Console.WriteLine($"Auto-detected: {path}");
        }

        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Not found: {path}");
            return 1;
        }

        _svc.Config.Rpcs3Path = Path.GetFullPath(path);
        _svc.Config.SetupComplete = true;
        _svc.SaveConfig();
        _svc.Paths.EnsurePes3Folders();
        Console.WriteLine($"Saved. PES3 folder: {_svc.Paths.Pes3Root}");
        return 0;
    }

    private static int ConfigCommand()
    {
        var c = _svc.Config;
        Console.WriteLine($"Config:     {Pes3Config.GetDefaultConfigPath()}");
        Console.WriteLine($"RPCS3:      {c.Rpcs3Path}");
        Console.WriteLine($"PES3 root:  {_svc.Paths.Pes3Root ?? "(unknown)"}");
        Console.WriteLine($"Library:    {_svc.Paths.LibraryRoot}");
        Console.WriteLine($"Legacy cache: {_svc.Paths.LegacyCacheRoot}");
        Console.WriteLine($"Storage mode: {Pes3StorageModeResolver.Resolve(c)}");
        Console.WriteLine($"Delete cache after play: {c.DeleteCacheAfterPlay}");
        Console.WriteLine($"Retail decrypt: {c.EnableRetailDecrypt}");
        Console.WriteLine($"Dump CLI:     {DiscDecryptor.FindDumpCliPath(c) ?? "(not found)"}");
        Console.WriteLine($"IRD dir:      {(string.IsNullOrWhiteSpace(c.IrdDir) ? PlatformPaths.DefaultIrdDirectory : c.IrdDir)}");
        return 0;
    }
}
