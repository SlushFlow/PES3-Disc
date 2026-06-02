using System.Text.Json;

namespace PES3Disc.Core;

public sealed class PromptedStore
{
    private readonly string _path;

    public PromptedStore(Pes3Paths paths) => _path = paths.PromptedVolumesPath;

    public HashSet<string> Load()
    {
        if (!File.Exists(_path))
            return new HashSet<string>(StringComparer.Ordinal);

        try
        {
            var json = File.ReadAllText(_path);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(list, StringComparer.Ordinal);
        }
        catch
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public void Save(HashSet<string> ids)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = ids.Count > 0 ? JsonSerializer.Serialize(ids.ToList()) : "[]";
        File.WriteAllText(_path, json);
    }
}
