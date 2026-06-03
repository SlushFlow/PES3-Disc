namespace PES3Disc.BugReports;

public enum DevStatusKind
{
    Green,
    Yellow,
    Grey,
}

public static class DevStatusKindExtensions
{
    public static string ToApiValue(this DevStatusKind kind) => kind switch
    {
        DevStatusKind.Green => "green",
        DevStatusKind.Yellow => "yellow",
        DevStatusKind.Grey => "grey",
        _ => "grey",
    };

    public static bool TryParse(string? value, out DevStatusKind kind)
    {
        kind = DevStatusKind.Grey;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        switch (value.Trim().ToLowerInvariant())
        {
            case "green":
                kind = DevStatusKind.Green;
                return true;
            case "yellow":
                kind = DevStatusKind.Yellow;
                return true;
            case "grey":
            case "gray":
                kind = DevStatusKind.Grey;
                return true;
            default:
                return false;
        }
    }
}
