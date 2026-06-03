namespace PES3Disc.BugReports;

public enum BugReportResolutionStatus
{
    Open,
    Declined,
    ToBeFixed,
    Fixed,
}

public static class BugReportResolutionStatusExtensions
{
    public static string ToApiValue(this BugReportResolutionStatus status) => status switch
    {
        BugReportResolutionStatus.Open => "open",
        BugReportResolutionStatus.Declined => "declined",
        BugReportResolutionStatus.ToBeFixed => "to_be_fixed",
        BugReportResolutionStatus.Fixed => "fixed",
        _ => "open",
    };

    public static bool TryParseApiValue(string? value, out BugReportResolutionStatus status)
    {
        status = BugReportResolutionStatus.Open;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        status = value.Trim().ToLowerInvariant() switch
        {
            "open" => BugReportResolutionStatus.Open,
            "declined" => BugReportResolutionStatus.Declined,
            "to_be_fixed" => BugReportResolutionStatus.ToBeFixed,
            "fixed" => BugReportResolutionStatus.Fixed,
            _ => BugReportResolutionStatus.Open,
        };
        return value.Trim().ToLowerInvariant() is "open" or "declined" or "to_be_fixed" or "fixed";
    }

    public static string UserHeading(this BugReportResolutionStatus status) => status switch
    {
        BugReportResolutionStatus.Declined => "Bug report declined",
        BugReportResolutionStatus.ToBeFixed => "Bug report accepted — fix planned",
        BugReportResolutionStatus.Fixed => "Bug report fixed",
        _ => "",
    };
}
