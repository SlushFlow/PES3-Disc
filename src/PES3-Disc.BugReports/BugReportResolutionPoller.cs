namespace PES3Disc.BugReports;

public sealed class BugReportResolutionPoller
{
    private bool _inProgress;

    public async Task<IReadOnlyList<BugReportUserNotification>> PollAsync(string? apiBaseUrl, CancellationToken ct = default)
    {
        if (_inProgress)
            return [];
        _inProgress = true;
        try
        {
            var pending = BugReportPendingTracker.Load().Where(e => !e.Notified).ToList();
            if (pending.Count == 0)
                return [];

            var url = string.IsNullOrWhiteSpace(apiBaseUrl)
                ? BugReportEndpoints.DefaultApiBaseUrl
                : apiBaseUrl.Trim();

            using var client = new BugReportClient(url);
            var notifications = new List<BugReportUserNotification>();

            foreach (var entry in pending)
            {
                BugReportResolutionDto? resolution;
                try
                {
                    resolution = await client.FetchResolutionAsync(entry.Id, ct);
                }
                catch
                {
                    continue;
                }

                if (resolution is null || resolution.ResolutionStatus == BugReportResolutionStatus.Open)
                    continue;

                notifications.Add(new BugReportUserNotification
                {
                    ReportId = entry.Id,
                    Title = string.IsNullOrWhiteSpace(resolution.Title) ? entry.Title : resolution.Title,
                    Status = resolution.ResolutionStatus,
                    Message = resolution.Message,
                });
            }

            return notifications;
        }
        finally
        {
            _inProgress = false;
        }
    }
}

public sealed class BugReportUserNotification
{
    public required string ReportId { get; init; }
    public required string Title { get; init; }
    public required BugReportResolutionStatus Status { get; init; }
    public string? Message { get; init; }

    public string FormatForUser()
    {
        var heading = Status.UserHeading();
        if (string.IsNullOrWhiteSpace(Message))
            return $"{heading}\n\nReport: {Title}";
        return $"{heading}\n\n{Message.Trim()}\n\nReport: {Title}";
    }
}
