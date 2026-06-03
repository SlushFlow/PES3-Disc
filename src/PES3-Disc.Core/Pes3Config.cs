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

    public static Pes3Config Load(string? path = null)
    {
        path ??= GetDefaultConfigPath();
        if (!File.Exists(path))
            return new Pes3Config();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Pes3Config>(json, JsonOptions) ?? new Pes3Config();
        }
        catch
        {
            return new Pes3Config();
        }
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
