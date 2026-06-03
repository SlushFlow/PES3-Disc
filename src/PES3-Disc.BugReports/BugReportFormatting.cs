namespace PES3Disc.BugReports;

public static class BugReportFormatting
{
    public static string FormatForClipboard(BugReportDto report)
    {
        return string.Join(Environment.NewLine, new[]
        {
            "PES3 REPORT",
            $"title: {report.Title}",
            $"problem: {report.Body}",
        });
    }
}
