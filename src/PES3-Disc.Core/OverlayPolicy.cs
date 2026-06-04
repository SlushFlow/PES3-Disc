namespace PES3Disc.Core;

public sealed class OverlayPolicy
{
    public int MaxLocalMegabytes { get; init; } = 2048;
    public long MaxLocalFileBytes { get; init; } = 4 * 1024 * 1024;
    public IReadOnlyList<string> AlwaysLocalPatterns { get; init; } = DefaultAlwaysLocalPatterns;

    public static readonly string[] DefaultAlwaysLocalPatterns =
    [
        "PS3_GAME/PARAM.SFO",
        "PS3_GAME/ICON0.PNG",
        "PS3_GAME/USRDIR/EBOOT.BIN",
        "PS3_GAME/USRDIR/EBOOT.BIN.SELF",
        "PS3_GAME/USRDIR/ICON0.PNG",
    ];

    public static OverlayPolicy FromConfig(Pes3Config config)
    {
        var mb = config.OverlayMaxLocalMegabytes;
        if (mb < 64)
            mb = 64;
        if (mb > 32_768)
            mb = 32_768;

        var patterns = config.OverlayAlwaysLocalPatterns is { Length: > 0 }
            ? config.OverlayAlwaysLocalPatterns
            : DefaultAlwaysLocalPatterns;

        return new OverlayPolicy
        {
            MaxLocalMegabytes = mb,
            AlwaysLocalPatterns = patterns,
        };
    }
}

public sealed class OverlayStats
{
    public long LocalBytes { get; init; }
    public int LocalFiles { get; init; }
    public int LinkedFiles { get; init; }
    public long LinkedBytes { get; init; }
    public bool UsedFullCopyFallback { get; init; }

    public string Summary
    {
        get
        {
            if (UsedFullCopyFallback)
                return "Full copy fallback (links unavailable).";
            return $"~{FormatBytes(LocalBytes)} on SSD, ~{FormatBytes(LinkedBytes)} from disc ({LinkedFiles} linked files).";
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.#} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024):0.#} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):0.#} GB";
    }
}
