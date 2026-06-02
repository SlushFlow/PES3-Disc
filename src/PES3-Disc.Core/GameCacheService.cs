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
        foreach (var dir in GetCacheSearchPaths(cacheRoot, volumeId, titleId, productCode))
        {
            var game = DiscDetector.FindGameOnDrive(dir + Path.DirectorySeparatorChar);
            if (game is not null)
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
                    if (game is not null)
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
        }

        return null;
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

        return new PlaySession
        {
            EbootPath = eboot,
            CleanupDirs = cleanup,
            FromCache = false,
            CacheDir = GameMetadata.GetGameRootFromEboot(eboot),
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
