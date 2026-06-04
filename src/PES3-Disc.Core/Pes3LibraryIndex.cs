using System.Text.Json;
using System.Text.Json.Serialization;

namespace PES3Disc.Core;

/// <summary>Title-level PES3 library index (product code / title id → install folder).</summary>
public sealed class Pes3LibraryIndex
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, LibraryIndexEntry> Titles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string IndexPath(string libraryRoot) =>
        Path.Combine(libraryRoot, ".pes3-library-index.json");

    public static Pes3LibraryIndex Load(string libraryRoot)
    {
        var path = IndexPath(libraryRoot);
        if (!File.Exists(path))
            return new Pes3LibraryIndex();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Pes3LibraryIndex>(json, JsonOptions) ?? new Pes3LibraryIndex();
        }
        catch
        {
            return new Pes3LibraryIndex();
        }
    }

    public void Save(string libraryRoot)
    {
        try
        {
            Directory.CreateDirectory(libraryRoot);
            File.WriteAllText(IndexPath(libraryRoot), JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Library index save failed: {ex.Message}");
        }
    }

    public void Upsert(string key, string installDir, string? title, Pes3LibraryTier tier)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        Titles[key] = new LibraryIndexEntry
        {
            InstallDir = installDir,
            Title = title,
            Tier = tier.ToString(),
            UpdatedUtc = DateTime.UtcNow,
        };
    }

    public string? TryGetInstallDir(string? key) =>
        string.IsNullOrWhiteSpace(key) || !Titles.TryGetValue(key, out var e)
            ? null
            : e.InstallDir;

    public void ImportLegacyCacheIndex(Pes3CacheIndex legacy, string libraryRoot)
    {
        foreach (var (code, entry) in legacy.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.CacheDir) || !Directory.Exists(entry.CacheDir))
                continue;

            var target = Pes3Paths.TitleInstallPath(libraryRoot, code);
            if (!string.Equals(entry.CacheDir, target, StringComparison.OrdinalIgnoreCase))
                Titles.TryAdd(code, new LibraryIndexEntry
                {
                    InstallDir = entry.CacheDir,
                    Title = entry.Title,
                    Tier = nameof(Pes3LibraryTier.PersistentLibrary),
                    UpdatedUtc = entry.UpdatedUtc,
                    MigratedFromLegacyCache = true,
                });
            else
                Upsert(code, target, entry.Title, Pes3LibraryTier.PersistentLibrary);
        }
    }
}

public sealed class LibraryIndexEntry
{
    public string InstallDir { get; set; } = "";
    public string? Title { get; set; }
    public string Tier { get; set; } = nameof(Pes3LibraryTier.PersistentLibrary);
    public DateTime UpdatedUtc { get; set; }
    public bool MigratedFromLegacyCache { get; set; }
}
