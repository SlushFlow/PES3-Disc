namespace PES3Disc.Core;

public sealed class Pes3Paths
{
    private readonly Pes3Config _config;

    public Pes3Paths(Pes3Config config) => _config = config;

    public string? Rpcs3InstallDir
    {
        get
        {
            if (!_config.IsRpcs3Configured)
                return null;
            return Path.GetDirectoryName(_config.Rpcs3Path);
        }
    }

    public string? Pes3Root
    {
        get
        {
            var rpcs3 = Rpcs3InstallDir;
            if (rpcs3 is null)
                return null;
            return Path.Combine(rpcs3, "PES3");
        }
    }

    public void EnsurePes3Folders()
    {
        var root = Pes3Root;
        if (root is null)
            return;

        foreach (var sub in new[] { "cache", "library", "logs", "state", "temp", "backups" })
            Directory.CreateDirectory(Path.Combine(root, sub));
        Directory.CreateDirectory(LibraryTitlesRoot);
    }

    public static string TitleInstallPath(string libraryRoot, string? key) =>
        Path.Combine(libraryRoot, "titles", GameMetadata.SanitizeCacheKey(key));

    public string LogPath =>
        Pes3Root is not null
            ? Path.Combine(Pes3Root, "logs", "disc-run.log")
            : Path.Combine(AppContext.BaseDirectory, "disc-run.log");

    public string PromptedVolumesPath =>
        Pes3Root is not null
            ? Path.Combine(Pes3Root, "state", "prompted-volumes.json")
            : Path.Combine(AppContext.BaseDirectory, "prompted-volumes.json");

    /// <summary>Legacy flat cache root (migration source + custom path override).</summary>
    public string LegacyCacheRoot => CacheRoot;

    public string CacheRoot
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_config.DumpCachePath))
            {
                Directory.CreateDirectory(_config.DumpCachePath);
                return _config.DumpCachePath;
            }

            if (Pes3Root is not null)
            {
                var cache = Path.Combine(Pes3Root, "cache");
                Directory.CreateDirectory(cache);
                return cache;
            }

            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PES3-Disc", "cache");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    /// <summary>Persistent title library (decrypt/copy targets, indexed replay).</summary>
    public string LibraryRoot
    {
        get
        {
            if (Pes3Root is not null)
            {
                var lib = Path.Combine(Pes3Root, "library");
                Directory.CreateDirectory(lib);
                return lib;
            }

            var libNextToCache = Path.Combine(CacheRoot, "..", "library");
            libNextToCache = Path.GetFullPath(libNextToCache);
            Directory.CreateDirectory(libNextToCache);
            return libNextToCache;
        }
    }

    public string LibraryTitlesRoot
    {
        get
        {
            var titles = Path.Combine(LibraryRoot, "titles");
            Directory.CreateDirectory(titles);
            return titles;
        }
    }

    public string TitleInstallDir(string? key) => TitleInstallPath(LibraryRoot, key);

    public string BackupRoot
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_config.BackupPath))
            {
                Directory.CreateDirectory(_config.BackupPath);
                return _config.BackupPath;
            }

            if (Pes3Root is not null)
            {
                var backups = Path.Combine(Pes3Root, "backups");
                Directory.CreateDirectory(backups);
                return backups;
            }

            var fallback = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PES3-Disc", "backups");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    public string NewSessionDir() => NewDirUnderTemp("session-");

    public string NewDiscOverlayDir() => NewDirUnderTemp("disc-overlay-");

    private string NewDirUnderTemp(string prefix)
    {
        var tempRoot = Pes3Root is not null
            ? Path.Combine(Pes3Root, "temp")
            : Path.Combine(Path.GetTempPath(), "PES3-Disc-sessions");
        Directory.CreateDirectory(tempRoot);
        var dir = Path.Combine(tempRoot, prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
