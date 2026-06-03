namespace PES3Disc.BugReports;

public static class BugReportLimits
{
    public const int MaxTitleLength = 50;
    public const int MaxBodyLength = 2500;
    public const int MaxResolutionMessageLength = 500;
    public const double ClusterSimilarityThreshold = 0.38;

    public static string ValidateResolutionMessage(string? message)
    {
        message = (message ?? "").Trim();
        if (message.Length > MaxResolutionMessageLength)
            throw new ArgumentException($"Message must be at most {MaxResolutionMessageLength} characters.");
        return message;
    }

    public static (string Title, string Body) Validate(string title, string body)
    {
        title = (title ?? "").Trim();
        body = (body ?? "").Trim();

        if (title.Length == 0)
            throw new ArgumentException("Title is required.");
        if (body.Length == 0)
            throw new ArgumentException("Description is required.");
        if (title.Length > MaxTitleLength)
            throw new ArgumentException($"Title must be at most {MaxTitleLength} characters.");
        if (body.Length > MaxBodyLength)
            throw new ArgumentException($"Description must be at most {MaxBodyLength} characters.");

        return (title, body);
    }
}
