using System.Text.Json;
using System.Text.Json.Serialization;

namespace PES3Disc.Core;

public sealed class Pes3Config
{
    public string Rpcs3Path { get; set; } = "";
    public int ScanDelaySeconds { get; set; } = 3;
    public bool UseNoGui { get; set; }
    public bool EnableRetailDecrypt { get; set; } = true;
    public bool DecryptUnknownOpticalMedia { get; set; }
    public bool DeleteCacheAfterPlay { get; set; }
    /// <summary>SmartHybrid | PersistentLibrary | EphemeralSession | DiscDirect. Empty = infer from <see cref="DeleteCacheAfterPlay"/>.</summary>
    public string StorageMode { get; set; } = "";
    public string DumpCachePath { get; set; } = "";
    public string DumpCliPath { get; set; } = "";
    public string IrdDir { get; set; } = "";
    public bool EnableBackups { get; set; } = true;
    public bool BackupSaves { get; set; } = true;
    public bool BackupOnLaunch { get; set; }
    public int MaxBackupsPerTitle { get; set; } = 3;
    public string BackupPath { get; set; } = "";
    public bool RunAtStartup { get; set; }
    public bool SetupComplete { get; set; }
    /// <summary>Matches <see cref="LegalTerms.CurrentVersion"/> when the user accepted the notice.</summary>
    public string? AcceptedLegalTermsVersion { get; set; }
    public DateTime? LegalTermsAcceptedUtc { get; set; }
    public string BugReportApiUrl { get; set; } = "";
    /// <summary>Remove disc-assisted session trees when the optical volume is ejected.</summary>
    public bool CleanupSessionsOnDiscEject { get; set; } = true;
    /// <summary>Max SSD bytes for small/critical files in a disc-assisted overlay (rest linked from disc).</summary>
    public int OverlayMaxLocalMegabytes { get; set; } = 2048;
    /// <summary>Optional relative paths always copied locally in overlay mode.</summary>
    public string[]? OverlayAlwaysLocalPatterns { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetDefaultConfigPath()
    {
        var portable = Path.Combine(AppContext.BaseDirectory, "config.json");
        if (File.Exists(portable))
            return portable;

        var dir = PlatformPaths.ConfigDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "config.json");
    }

    public static Pes3Config Load(string? path = null) => LoadWithDiagnostics(path).Config;

    public static Pes3ConfigLoadResult LoadWithDiagnostics(string? path = null)
    {
        path ??= GetDefaultConfigPath();
        if (!File.Exists(path))
            return new Pes3ConfigLoadResult { Config = new Pes3Config(), LoadedFromDisk = false };

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<Pes3Config>(json, JsonOptions) ?? new Pes3Config();
            var warning = Validate(config);
            return new Pes3ConfigLoadResult
            {
                Config = config,
                LoadedFromDisk = true,
                Warning = warning,
            };
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Config load failed ({path}): {ex.Message}");
            return new Pes3ConfigLoadResult
            {
                Config = new Pes3Config(),
                LoadedFromDisk = true,
                Warning = "Settings file could not be read; defaults are in use. Re-save Settings to repair config.json.",
            };
        }
    }

    public static string? Validate(Pes3Config config)
    {
        if (!string.IsNullOrWhiteSpace(config.Rpcs3Path) && !File.Exists(config.Rpcs3Path))
            return "RPCS3 path in settings does not exist.";

        if (!string.IsNullOrWhiteSpace(config.StorageMode)
            && !Pes3StorageModeResolver.TryParse(config.StorageMode, out _))
            return $"Unknown storage mode \"{config.StorageMode}\"; using SmartHybrid.";

        if (config.ScanDelaySeconds < 1)
            config.ScanDelaySeconds = 1;
        else if (config.ScanDelaySeconds > 60)
            config.ScanDelaySeconds = 60;

        if (config.OverlayMaxLocalMegabytes < 256)
            config.OverlayMaxLocalMegabytes = 256;

        return null;
    }

    public void Save(string? path = null)
    {
        path ??= GetDefaultConfigPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(path, json);
    }

    public bool IsRpcs3Configured =>
        !string.IsNullOrWhiteSpace(Rpcs3Path) && File.Exists(Rpcs3Path);
}
