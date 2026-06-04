namespace PES3Disc.Core;

public static class GameMetadata
{
    public static string? GetGameRootFromEboot(string ebootPath)
    {
        try
        {
            var usrDir = Directory.GetParent(ebootPath)?.FullName;
            var ps3Game = Directory.GetParent(usrDir ?? "")?.FullName;
            return Directory.GetParent(ps3Game ?? "")?.FullName;
        }
        catch
        {
            return null;
        }
    }

    public static string SanitizeCacheKey(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return "UNKNOWN";
        var safe = string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        if (safe.Length > 64)
            safe = safe[..64];
        return string.IsNullOrEmpty(safe) ? "UNKNOWN" : safe;
    }

    public static string? TryReadProductCodeFromVolume(string driveRoot)
    {
        if (string.IsNullOrWhiteSpace(driveRoot))
            return null;

        if (!driveRoot.EndsWith(Path.DirectorySeparatorChar) && !driveRoot.EndsWith('/'))
            driveRoot += Path.DirectorySeparatorChar;

        foreach (var sfo in new[]
                 {
                     Path.Combine(driveRoot, "PS3_GAME", "PARAM.SFO"),
                     Path.Combine(driveRoot, "dev_bdvd", "PS3_GAME", "PARAM.SFO"),
                 })
        {
            var fields = ParamSfo.ReadFields(sfo);
            if (fields.TryGetValue("TITLE_ID", out var id) && !string.IsNullOrWhiteSpace(id))
                return SanitizeCacheKey(id);
        }

        return null;
    }

    public static (string TitleId, string Title) ReadTitleFromEboot(string ebootPath)
    {
        var ps3Game = Directory.GetParent(Directory.GetParent(ebootPath)!.FullName)!.FullName;
        var sfo = Path.Combine(ps3Game, "PARAM.SFO");
        var fields = ParamSfo.ReadFields(sfo);
        var titleId = fields.GetValueOrDefault("TITLE_ID");
        var title = fields.GetValueOrDefault("TITLE");
        var gameRoot = GetGameRootFromEboot(ebootPath);

        if (string.IsNullOrWhiteSpace(titleId))
            titleId = gameRoot is not null ? Path.GetFileName(gameRoot) : "UNKNOWN";
        if (string.IsNullOrWhiteSpace(title))
            title = titleId;

        return (SanitizeCacheKey(titleId), title);
    }
}
