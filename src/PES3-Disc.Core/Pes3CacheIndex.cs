using System.Text.Json;
using System.Text.Json.Serialization;

namespace PES3Disc.Core;

/// <summary>Persistent map of retail product code → decrypted cache folder.</summary>
public sealed class Pes3CacheIndex
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Dictionary<string, CacheIndexEntry> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string IndexPath(string cacheRoot) => Path.Combine(cacheRoot, ".pes3-cache-index.json");

    public static Pes3CacheIndex Load(string cacheRoot)
    {
        var path = IndexPath(cacheRoot);
        if (!File.Exists(path))
            return new Pes3CacheIndex();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<Pes3CacheIndex>(json, JsonOptions) ?? new Pes3CacheIndex();
        }
        catch
        {
            return new Pes3CacheIndex();
        }
    }

    public void Save(string cacheRoot)
    {
        try
        {
            Directory.CreateDirectory(cacheRoot);
            var path = IndexPath(cacheRoot);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Cache index save failed: {ex.Message}");
        }
    }

    public void Upsert(string? productCode, string cacheDir, string? title)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            return;

        Entries[productCode] = new CacheIndexEntry
        {
            CacheDir = cacheDir,
            Title = title,
            UpdatedUtc = DateTime.UtcNow,
        };
    }

    public string? TryGetCacheDir(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            return null;
        return Entries.TryGetValue(productCode, out var e) ? e.CacheDir : null;
    }
}

public sealed class CacheIndexEntry
{
    public string CacheDir { get; set; } = "";
    public string? Title { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
