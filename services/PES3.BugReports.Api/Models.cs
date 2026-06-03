namespace PES3.BugReports.Api;

public sealed record ReportRecord(
    string Id,
    string ClusterId,
    string Title,
    string Body,
    string Platform,
    string AppVersion,
    string OsDescription,
    DateTime CreatedAtUtc);

public sealed record ClusterRecord(
    string Id,
    string SummaryTitle,
    int ReportCount,
    DateTime UpdatedAtUtc);

public sealed record SubmitReportRequest(
    string Title,
    string Body,
    string Platform,
    string AppVersion,
    string OsDescription);

public sealed record SubmitReportResponse(string Id, string ClusterId);

public sealed record ReportDto(
    string Id,
    string ClusterId,
    string Title,
    string Body,
    string Platform,
    string AppVersion,
    string OsDescription,
    DateTime CreatedAtUtc);

public sealed record ClusterSummaryDto(
    string ClusterId,
    string SummaryTitle,
    int ReportCount,
    DateTime UpdatedAtUtc,
    IReadOnlyList<ReportDto> Reports);
