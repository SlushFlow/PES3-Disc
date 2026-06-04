using PES3Disc.Core;

namespace PES3Disc.ViewModels;

public sealed class Pes3Services
{
    public Pes3Config Config { get; private set; } = new();
    public Pes3Paths Paths { get; private set; } = null!;
    public GameCacheService Cache { get; private set; } = null!;
    public Pes3BackupService Backup { get; private set; } = null!;
    public Rpcs3Launcher Launcher { get; private set; } = null!;
    public DiscDecryptor Decryptor { get; private set; } = null!;
    public PromptedStore Prompted { get; private set; } = null!;
    public PlaySessionRegistry SessionRegistry { get; private set; } = null!;
    public string ConfigPath { get; private set; } = "";

    public static Pes3Services Load()
    {
        var configPath = Pes3Config.GetDefaultConfigPath();
        var config = Pes3Config.Load(configPath);
        var paths = new Pes3Paths(config);
        var backup = new Pes3BackupService(config, paths);
        var registry = new PlaySessionRegistry(paths);
        return new Pes3Services
        {
            Config = config,
            ConfigPath = configPath,
            Paths = paths,
            Cache = new GameCacheService(config, paths),
            Backup = backup,
            Launcher = new Rpcs3Launcher(config, paths, backup, registry),
            Decryptor = new DiscDecryptor(config),
            Prompted = new PromptedStore(paths),
            SessionRegistry = registry,
        };
    }

    public void Initialize()
    {
        Paths.EnsurePes3Folders();
        Pes3Log.SetPath(Paths.LogPath);
        Cache.EnsureLibraryReady();
        if (Config.CleanupSessionsOnDiscEject)
            SessionRegistry.ReconcileOnStartup();
    }

    public void SaveConfig()
    {
        Config.Save(ConfigPath);
        Paths = new Pes3Paths(Config);
        Backup = new Pes3BackupService(Config, Paths);
        SessionRegistry = new PlaySessionRegistry(Paths);
        Cache = new GameCacheService(Config, Paths);
        Launcher = new Rpcs3Launcher(Config, Paths, Backup, SessionRegistry);
        Decryptor = new DiscDecryptor(Config);
        Prompted = new PromptedStore(Paths);
        Initialize();
    }
}
