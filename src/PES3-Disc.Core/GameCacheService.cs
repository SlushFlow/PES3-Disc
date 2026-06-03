namespace PES3Disc.Core;

public sealed class CachedGameEntry
{
    public required string CacheDir { get; init; }
    public required DetectedGame Game { get; init; }
}

public sealed class PlaySession
{
    public required string EbootPath { get; init; }
    public required IReadOnlyList<string> CleanupDirs { get; init; }
    public bool FromCache { get; init; }
    public string? CacheDir { get; init; }
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
/// Unified PES3 cache for retail decrypt output and DIY disc staging (fast RPCS3 I/O from SSD).
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

    public CachedGameEntry? TryGetCached(string? volumeId, string? titleId, string? productCode)
    {
        if (_config.DeleteCacheAfterPlay)
            return null;

        var cacheRoot = _paths.CacheRoot;
        var index = Pes3CacheIndex.Load(cacheRoot);

        if (!string.IsNullOrWhiteSpace(productCode))
        {
            var indexedDir = index.TryGetCacheDir(productCode);
            if (indexedDir is not null)
            {
                var indexed = TryGameAt(indexedDir);
                if (indexed is not null)
                    return indexed;
            }
        }

        foreach (var dir in GetCacheSearchPaths(cacheRoot, volumeId, titleId, productCode))
        {
            var hit = TryGameAt(dir);
            if (hit is not null)
                return hit;
        }

        return null;
    }

    /// <summary>If the index has exactly one playable retail cache, return it (helps before disc probe).</summary>
    public CachedGameEntry? TryGetSoleIndexedRetail()
    {
        if (_config.DeleteCacheAfterPlay)
            return null;

        CachedGameEntry? sole = null;
        var index = Pes3CacheIndex.Load(_paths.CacheRoot);
        foreach (var entry in index.Entries.Values)
        {
            var hit = TryGameAt(entry.CacheDir);
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
        if (_config.DeleteCacheAfterPlay)
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

    private static CachedGameEntry? TryGameAt(string dir)
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
        var titleId = game.TitleId ?? GameMetadata.ReadTitleFromEboot(game.EbootPath).TitleId;

        if (!_config.DeleteCacheAfterPlay)
        {
            var cached = TryGetCached(drive.Id, titleId, null);
            if (cached is not null)
            {
                Pes3Log.Write($"DIY cache hit: {cached.CacheDir}");
                return new PlaySession
                {
                    EbootPath = cached.Game.EbootPath,
                    CleanupDirs = Array.Empty<string>(),
                    FromCache = true,
                    CacheDir = cached.CacheDir,
                };
            }
        }

        var gameRoot = game.GameRoot ?? GameMetadata.GetGameRootFromEboot(game.EbootPath);
        if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
            throw new InvalidOperationException("Could not locate the game folder on the disc.");

        string cacheDir;
        IReadOnlyList<string> cleanup;

        if (_config.DeleteCacheAfterPlay)
        {
            cacheDir = _paths.NewSessionDir();
            cleanup = new[] { cacheDir };
        }
        else
        {
            cacheDir = Path.Combine(_paths.CacheRoot, GameMetadata.SanitizeCacheKey(titleId));
            if (Directory.Exists(cacheDir))
            {
                try { Directory.Delete(cacheDir, recursive: true); } catch { /* ignore */ }
            }
            Directory.CreateDirectory(cacheDir);
            cleanup = Array.Empty<string>();
        }

        progress?.Report(new StageProgress { Status = "Preparing cache…", FilesCopied = 0, TotalFiles = 0 });
        var ok = await DirectoryStaging.CopyTreeAsync(gameRoot, cacheDir, progress, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            throw new InvalidOperationException("Failed to copy the disc to the PES3 cache.");

        var eboot = Path.Combine(cacheDir, "PS3_GAME", "USRDIR", "EBOOT.BIN");
        if (!File.Exists(eboot))
        {
            var found = DiscDetector.FindGameOnDrive(cacheDir + Path.DirectorySeparatorChar);
            if (found is null)
                throw new InvalidOperationException("EBOOT.BIN not found after caching.");
            eboot = found.EbootPath;
        }

        Pes3Log.Write($"DIY staged to cache: {cacheDir}");
        return new PlaySession
        {
            EbootPath = eboot,
            CleanupDirs = cleanup,
            FromCache = false,
            CacheDir = cacheDir,
        };
    }

    public PlaySession SessionFromCached(CachedGameEntry cached) => new()
    {
        EbootPath = cached.Game.EbootPath,
        CleanupDirs = Array.Empty<string>(),
        FromCache = true,
        CacheDir = cached.CacheDir,
    };

    public string ResolveRetailOutputDir(string? productCode)
    {
        if (_config.DeleteCacheAfterPlay)
            return _paths.NewSessionDir();

        if (!string.IsNullOrWhiteSpace(productCode))
            return Path.Combine(_paths.CacheRoot, GameMetadata.SanitizeCacheKey(productCode));

        return Path.Combine(_paths.CacheRoot, $"dump-{DateTime.Now:yyyyMMdd-HHmmss}");
    }

    public PlaySession FinalizeRetailDecrypt(
        DecryptResult result,
        string outputDir,
        List<string> cleanup)
    {
        var eboot = result.Eboot!;
        if (!_config.DeleteCacheAfterPlay && !string.IsNullOrEmpty(result.ProductCode) && result.GameRoot is not null)
        {
            var final = Path.Combine(_paths.CacheRoot, GameMetadata.SanitizeCacheKey(result.ProductCode));
            if (Directory.Exists(final))
            {
                try { Directory.Delete(final, true); } catch { /* ignore */ }
            }
            try
            {
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
        else if (result.GameRoot is not null)
        {
            cleanup.Add(result.GameRoot);
        }

        var cacheDir = GameMetadata.GetGameRootFromEboot(eboot);
        if (!_config.DeleteCacheAfterPlay && !string.IsNullOrEmpty(result.ProductCode) && cacheDir is not null)
        {
            var index = Pes3CacheIndex.Load(_paths.CacheRoot);
            index.Upsert(result.ProductCode, cacheDir, result.Title);
            index.Save(_paths.CacheRoot);
        }

        return new PlaySession
        {
            EbootPath = eboot,
            CleanupDirs = cleanup,
            FromCache = false,
            CacheDir = cacheDir,
        };
    }

    private static IEnumerable<string> GetCacheSearchPaths(
        string cacheRoot,
        string? volumeId,
        string? titleId,
        string? productCode)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in new[] { productCode, titleId })
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;
            var dir = Path.Combine(cacheRoot, GameMetadata.SanitizeCacheKey(key));
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
