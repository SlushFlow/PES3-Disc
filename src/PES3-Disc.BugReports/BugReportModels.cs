namespace PES3Disc.BugReports;

public sealed class BugReportSubmission
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public required string Platform { get; init; }
    public required string AppVersion { get; init; }
    public required string OsDescription { get; init; }
}

public sealed class BugReportSubmitResult
{
    public required string Id { get; init; }
    public required string ClusterId { get; init; }
}

public sealed class BugReportDto
{
    public string Id { get; set; } = "";
    public string ClusterId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Platform { get; set; } = "";
    public string AppVersion { get; set; } = "";
    public string OsDescription { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = "open";
    public string? ResolutionMessage { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public BugReportResolutionStatus ResolutionStatus =>
        BugReportResolutionStatusExtensions.TryParseApiValue(Status, out var s) ? s : BugReportResolutionStatus.Open;

    public bool IsOpen => ResolutionStatus == BugReportResolutionStatus.Open;
}

public sealed class BugReportResolveRequest
{
    public required string Status { get; init; }
    public string? Message { get; init; }
}

public sealed class BugReportResolutionDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "open";
    public string? Message { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public BugReportResolutionStatus ResolutionStatus =>
        BugReportResolutionStatusExtensions.TryParseApiValue(Status, out var s) ? s : BugReportResolutionStatus.Open;
}

public sealed class BugReportClusterSummary
{
    public string ClusterId { get; set; } = "";
    public string SummaryTitle { get; set; } = "";
    public int ReportCount { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<BugReportDto> Reports { get; set; } = [];
}

public sealed class ClusterMatchCandidate
{
    public required string ClusterId { get; init; }
    public required string SummaryTitle { get; init; }
    public required string CentroidTitle { get; init; }
    public required string CentroidBody { get; init; }
}
