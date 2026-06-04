namespace PES3Disc.Core;

public sealed class CachedGameEntry
{
    public required string CacheDir { get; init; }
    public required DetectedGame Game { get; init; }
    public Pes3LibraryTier Tier { get; init; } = Pes3LibraryTier.PersistentLibrary;
}

public sealed class PlaySession
{
    public required string EbootPath { get; init; }
    public required IReadOnlyList<string> CleanupDirs { get; init; }
    public bool FromCache { get; init; }
    public string? CacheDir { get; init; }
    public Pes3LibraryTier Tier { get; init; } = Pes3LibraryTier.PersistentLibrary;
    public string? VolumeId { get; init; }
    public string? DiscRoot { get; init; }
    public OverlayStats? OverlayStats { get; init; }
}

public sealed class StageProgress
{
    public string? Status { get; init; }
    public int FilesCopied { get; init; }
    public int TotalFiles { get; init; }

    public int Percent => TotalFiles > 0
        ? (int)Math.Clamp(100 * FilesCopied / TotalFiles, 0, 100)
        : 0;
}

/// <summary>
/// PES3 library: tiered storage (session, persistent library, disc reference) for DIY copy and retail decrypt.
/// </summary>
public sealed class GameCacheService
{
    private readonly Pes3Config _config;
    private readonly Pes3Paths _paths;

    public GameCacheService(Pes3Config config, Pes3Paths paths)
    {
        _config = config;
        _paths = paths;
    }

    public void EnsureLibraryReady() => Pes3LibraryMigrator.MigrateIfNeeded(_paths);

    public CachedGameEntry? TryGetCached(string? volumeId, string? titleId, string? productCode)
    {
        if (!Pes3StorageModeResolver.KeepsPersistentLibrary(_config))
            return null;

        EnsureLibraryReady();
        var libraryRoot = _paths.LibraryRoot;
        var libIndex = Pes3LibraryIndex.Load(libraryRoot);

        foreach (var key in UniqueKeys(productCode, titleId))
        {
            var indexedDir = libIndex.TryGetInstallDir(key);
            if (indexedDir is not null)
            {
                var hit = TryGameAt(indexedDir, Pes3LibraryTier.PersistentLibrary);
                if (hit is not null)
                    return hit;
            }

            var titleDir = _paths.TitleInstallDir(key);
            var titleHit = TryGameAt(titleDir, Pes3LibraryTier.PersistentLibrary);
            if (titleHit is not null)
                return titleHit;
        }

        var cacheRoot = _paths.LegacyCacheRoot;
        var legacyIndex = Pes3CacheIndex.Load(cacheRoot);
        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var indexedDir = legacyIndex.TryGetCacheDir(productCode);
            if (indexedDir is not null)
            {
                var indexed = TryGameAt(indexedDir, Pes3LibraryTier.PersistentLibrary);
                if (indexed is not null)
                    return indexed;
            }
        }

        foreach (var dir in GetLegacySearchPaths(cacheRoot, volumeId, titleId, productCode))
        {
            var hit = TryGameAt(dir, Pes3LibraryTier.PersistentLibrary);
            if (hit is not null)
                return hit;
        }

        return null;
    }

    public CachedGameEntry? TryGetSoleIndexedRetail()
    {
        if (!Pes3StorageModeResolver.KeepsPersistentLibrary(_config))
            return null;

        EnsureLibraryReady();
        CachedGameEntry? sole = null;
        var libIndex = Pes3LibraryIndex.Load(_paths.LibraryRoot);
        foreach (var entry in libIndex.Titles.Values)
        {
            var hit = TryGameAt(entry.InstallDir, Pes3LibraryTier.PersistentLibrary);
            if (hit is null)
                continue;
            if (sole is not null)
                return null;
            sole = hit;
        }

        if (sole is not null)
            return sole;

        var legacy = Pes3CacheIndex.Load(_paths.LegacyCacheRoot);
        foreach (var entry in legacy.Entries.Values)
        {
            var hit = TryGameAt(entry.CacheDir, Pes3LibraryTier.PersistentLibrary);
            if (hit is null)
                continue;
            if (sole is not null)
                return null;
            sole = hit;
        }

        return sole;
    }

    public async Task<CachedGameEntry?> TryGetRetailCachedAsync(
        OpticalDrive drive,
        Func<CancellationToken, Task<DiscProbeResult?>>? probeAsync,
        CancellationToken cancellationToken = default)
    {
        if (!Pes3StorageModeResolver.KeepsPersistentLibrary(_config))
            return null;

        string? productCode = null;
        if (probeAsync is not null)
        {
            try
            {
                var probe = await probeAsync(cancellationToken).ConfigureAwait(false);
                productCode = probe?.ProductCode;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // ignore probe errors
            }
        }

        var hit = TryGetCached(drive.Id, null, productCode);
        if (hit is not null)
            return hit;

        return TryGetSoleIndexedRetail();
    }

    private static CachedGameEntry? TryGameAt(string dir, Pes3LibraryTier tier)
    {
        if (!Directory.Exists(dir))
            return null;

        var game = DiscDetector.FindGameOnDrive(dir + Path.DirectorySeparatorChar);
        if (game is not null && !IsEncryptedRetailEboot(game.EbootPath))
        {
            return new CachedGameEntry
            {
                CacheDir = dir,
                Game = game,
                Tier = tier,
            };
        }

        try
        {
            foreach (var sub in Directory.EnumerateDirectories(dir).Take(32))
            {
                game = DiscDetector.FindGameOnDrive(sub + Path.DirectorySeparatorChar);
                if (game is not null && !IsEncryptedRetailEboot(game.EbootPath))
                {
                    return new CachedGameEntry
                    {
                        CacheDir = sub,
                        Game = game,
                        Tier = tier,
                    };
                }
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static bool IsEncryptedRetailEboot(string ebootPath)
    {
        try
        {
            using var fs = File.OpenRead(ebootPath);
            Span<byte> buf = stackalloc byte[7];
            if (fs.Read(buf) < 7)
                return false;
            return buf[0] == 0x53 && buf[1] == 0x43 && buf[2] == 0x45 && buf[6] == 2;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PlaySession> PrepareDiyPlayAsync(
        OpticalDrive drive,
        DetectedGame game,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureLibraryReady();
        var mode = Pes3StorageModeResolver.Resolve(_config);
        var titleId = game.TitleId ?? GameMetadata.ReadTitleFromEboot(game.EbootPath).TitleId;

        if (Pes3StorageModeResolver.KeepsPersistentLibrary(_config))
        {
            var cached = TryGetCached(drive.Id, titleId, null);
            if (cached is not null)
            {
                Pes3Log.Write($"DIY library hit ({cached.Tier}): {cached.CacheDir}");
                return SessionFromCached(cached);
            }
        }

        if (mode == Pes3StorageMode.DiscDirect)
        {
            if (!IsEncryptedRetailEboot(game.EbootPath) && File.Exists(game.EbootPath))
            {
                Pes3Log.Write($"DIY play from disc reference: {game.EbootPath}");
                return new PlaySession
                {
                    EbootPath = game.EbootPath,
                    CleanupDirs = Array.Empty<string>(),
                    FromCache = false,
                    CacheDir = game.GameRoot,
                    Tier = Pes3LibraryTier.DiscReference,
                    VolumeId = drive.Id,
                    DiscRoot = drive.Root,
                };
            }
        }

        if (mode == Pes3StorageMode.SmartHybrid)
        {
            var overlaySession = await PrepareDiscAssistedOverlayAsync(
                drive, game, progress, cancellationToken).ConfigureAwait(false);
            if (overlaySession is not null)
                return overlaySession;
        }

        var gameRoot = game.GameRoot ?? GameMetadata.GetGameRootFromEboot(game.EbootPath);
        if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
            throw new InvalidOperationException("Could not locate the game folder on the disc.");

        string installDir;
        IReadOnlyList<string> cleanup;
        Pes3LibraryTier tier;

        if (mode is Pes3StorageMode.EphemeralSession or Pes3StorageMode.SmartHybrid)
        {
            installDir = mode == Pes3StorageMode.SmartHybrid
                ? _paths.NewDiscOverlayDir()
                : _paths.NewSessionDir();
            cleanup = new[] { installDir };
            tier = mode == Pes3StorageMode.SmartHybrid
                ? Pes3LibraryTier.DiscAssistedOverlay
                : Pes3LibraryTier.EphemeralSession;
        }
        else
        {
            installDir = _paths.TitleInstallDir(titleId);
            if (Directory.Exists(installDir))
            {
                try { Directory.Delete(installDir, recursive: true); } catch { /* ignore */ }
            }
            Directory.CreateDirectory(installDir);
            cleanup = Array.Empty<string>();
            tier = Pes3LibraryTier.PersistentLibrary;
        }

        progress?.Report(new StageProgress
        {
            Status = mode == Pes3StorageMode.PersistentLibrary
                ? "Adding to PES3 library…"
                : "Preparing session (full copy)…",
            FilesCopied = 0,
            TotalFiles = 0,
        });

        var ok = await DirectoryStaging.CopyTreeAsync(gameRoot, installDir, progress, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            throw new InvalidOperationException("Failed to copy the disc into the PES3 session.");

        var eboot = Path.Combine(installDir, "PS3_GAME", "USRDIR", "EBOOT.BIN");
        if (!File.Exists(eboot))
        {
            var found = DiscDetector.FindGameOnDrive(installDir + Path.DirectorySeparatorChar);
            if (found is null)
                throw new InvalidOperationException("EBOOT.BIN not found after library staging.");
            eboot = found.EbootPath;
        }

        if (tier == Pes3LibraryTier.PersistentLibrary)
        {
            var libIndex = Pes3LibraryIndex.Load(_paths.LibraryRoot);
            libIndex.Upsert(titleId, installDir, game.Title, tier);
            libIndex.Save(_paths.LibraryRoot);
        }

        Pes3Log.Write($"DIY staged to library ({tier}): {installDir}");
        return new PlaySession
        {
            EbootPath = eboot,
            CleanupDirs = cleanup,
            FromCache = false,
            CacheDir = installDir,
            Tier = tier,
            VolumeId = drive.Id,
            DiscRoot = drive.Root,
        };
    }

    public async Task<PlaySession?> PrepareDiscAssistedOverlayAsync(
        OpticalDrive drive,
        DetectedGame game,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (IsEncryptedRetailEboot(game.EbootPath))
            return null;

        var gameRoot = game.GameRoot ?? GameMetadata.GetGameRootFromEboot(game.EbootPath);
        if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
            return null;

        var sessionRoot = _paths.NewDiscOverlayDir();
        var materializer = new DiscOverlayMaterializer(OverlayPolicy.FromConfig(_config));
        DiscOverlayResult result;
        try
        {
            result = await materializer.BuildAsync(gameRoot, sessionRoot, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            try { SessionCleanup.DeleteTree(sessionRoot); } catch { /* ignore */ }
            throw;
        }

        Pes3Log.Write($"DIY disc-assisted overlay: {result.Stats?.Summary ?? sessionRoot}");
        return new PlaySession
        {
            EbootPath = result.EbootPath,
            CleanupDirs = new[] { sessionRoot },
            FromCache = false,
            CacheDir = sessionRoot,
            Tier = Pes3LibraryTier.DiscAssistedOverlay,
            VolumeId = drive.Id,
            DiscRoot = drive.Root,
            OverlayStats = result.Stats,
        };
    }

    public PlaySession SessionFromCached(CachedGameEntry cached) => new()
    {
        EbootPath = cached.Game.EbootPath,
        CleanupDirs = Array.Empty<string>(),
        FromCache = true,
        CacheDir = cached.CacheDir,
        Tier = cached.Tier,
    };

    public string ResolveRetailOutputDir(string? productCode)
    {
        var mode = Pes3StorageModeResolver.Resolve(_config);
        if (mode is Pes3StorageMode.EphemeralSession or Pes3StorageMode.SmartHybrid)
            return _paths.NewSessionDir();

        if (!string.IsNullOrWhiteSpace(productCode))
            return _paths.TitleInstallDir(productCode);

        return Path.Combine(_paths.LibraryTitlesRoot, $"dump-{DateTime.Now:yyyyMMdd-HHmmss}");
    }

    public PlaySession FinalizeRetailDecrypt(
        DecryptResult result,
        string outputDir,
        List<string> cleanup)
    {
        var eboot = result.Eboot!;
        var mode = Pes3StorageModeResolver.Resolve(_config);

        if (mode == Pes3StorageMode.PersistentLibrary
            && !string.IsNullOrEmpty(result.ProductCode)
            && result.GameRoot is not null)
        {
            var final = _paths.TitleInstallDir(result.ProductCode);
            if (Directory.Exists(final))
            {
                try { Directory.Delete(final, true); } catch { /* ignore */ }
            }
            try
            {
                if (!string.Equals(result.GameRoot, final, StringComparison.OrdinalIgnoreCase))
                    Directory.Move(result.GameRoot, final);
                eboot = Path.Combine(final, "PS3_GAME", "USRDIR", "EBOOT.BIN");
                if (!File.Exists(eboot))
                {
                    var g = DiscDetector.FindGameOnDrive(final + Path.DirectorySeparatorChar);
                    if (g is not null)
                        eboot = g.EbootPath;
                }
                cleanup.Clear();
            }
            catch
            {
                cleanup.Add(result.GameRoot);
            }
        }
        else if (result.GameRoot is not null && cleanup.Count == 0)
        {
            cleanup.Add(result.GameRoot);
        }
        else if (result.GameRoot is not null && !cleanup.Contains(result.GameRoot, StringComparer.OrdinalIgnoreCase))
        {
            cleanup.Add(result.GameRoot);
        }

        var installDir = GameMetadata.GetGameRootFromEboot(eboot);
        if (mode == Pes3StorageMode.PersistentLibrary
            && !string.IsNullOrEmpty(result.ProductCode)
            && installDir is not null)
        {
            var libIndex = Pes3LibraryIndex.Load(_paths.LibraryRoot);
            libIndex.Upsert(result.ProductCode, installDir, result.Title, Pes3LibraryTier.PersistentLibrary);
            libIndex.Save(_paths.LibraryRoot);

            var legacy = Pes3CacheIndex.Load(_paths.LegacyCacheRoot);
            legacy.Upsert(result.ProductCode, installDir, result.Title);
            legacy.Save(_paths.LegacyCacheRoot);
        }

        var tier = mode == Pes3StorageMode.PersistentLibrary
            ? Pes3LibraryTier.PersistentLibrary
            : Pes3LibraryTier.EphemeralSession;

        return new PlaySession
        {
            EbootPath = eboot,
            CleanupDirs = cleanup,
            FromCache = false,
            CacheDir = installDir,
            Tier = tier,
        };
    }

    private static IEnumerable<string> UniqueKeys(params string?[] keys)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            var safe = GameMetadata.SanitizeCacheKey(key);
            if (seen.Add(safe))
                yield return safe;
        }
    }

    private static IEnumerable<string> GetLegacySearchPaths(
        string cacheRoot,
        string? volumeId,
        string? titleId,
        string? productCode)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in UniqueKeys(productCode, titleId))
        {
            var dir = Path.Combine(cacheRoot, key);
            if (seen.Add(dir))
                yield return dir;
        }

        if (!string.IsNullOrWhiteSpace(volumeId))
        {
            var dir = Path.Combine(cacheRoot, GameMetadata.SanitizeCacheKey(volumeId));
            if (seen.Add(dir))
                yield return dir;
        }
    }
}
