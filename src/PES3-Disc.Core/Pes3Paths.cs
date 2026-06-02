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

        foreach (var sub in new[] { "cache", "logs", "state", "temp", "backups" })
            Directory.CreateDirectory(Path.Combine(root, sub));
    }

    public string LogPath =>
        Pes3Root is not null
            ? Path.Combine(Pes3Root, "logs", "disc-run.log")
            : Path.Combine(AppContext.BaseDirectory, "disc-run.log");

    public string PromptedVolumesPath =>
        Pes3Root is not null
            ? Path.Combine(Pes3Root, "state", "prompted-volumes.json")
            : Path.Combine(AppContext.BaseDirectory, "prompted-volumes.json");

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

    public string NewSessionDir()
    {
        var tempRoot = Pes3Root is not null
            ? Path.Combine(Pes3Root, "temp")
            : Path.Combine(Path.GetTempPath(), "PES3-Disc-sessions");
        Directory.CreateDirectory(tempRoot);
        var dir = Path.Combine(tempRoot, "session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
