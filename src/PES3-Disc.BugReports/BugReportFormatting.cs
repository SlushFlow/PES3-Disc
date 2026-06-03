namespace PES3Disc.BugReports;

public static class BugReportFormatting
{
    public static string FormatForClipboard(BugReportDto report)
    {
        var lines = new List<string>
        {
            $"Title: {report.Title}",
            $"Platform: {report.Platform} • v{report.AppVersion}",
            $"OS: {report.OsDescription}",
            $"Submitted: {report.CreatedAtUtc.ToLocalTime():g}",
            $"Report ID: {report.Id}",
            "",
            report.Body,
        };
        return string.Join(Environment.NewLine, lines);
    }
}
