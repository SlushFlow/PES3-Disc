using System.Text.RegularExpressions;

namespace PES3.BugReports.Api;

public static partial class ReportClustering
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with",
        "is", "it", "this", "that", "when", "i", "my", "cant", "cannot", "not", "get", "does",
    };

    public const int MaxTitleLength = 50;
    public const int MaxBodyLength = 2500;
    public const double ClusterSimilarityThreshold = 0.38;

    public static (string Title, string Body) Validate(string title, string body)
    {
        title = (title ?? "").Trim();
        body = (body ?? "").Trim();
        if (title.Length == 0 || body.Length == 0)
            throw new ArgumentException("Title and body are required.");
        if (title.Length > MaxTitleLength)
            throw new ArgumentException($"Title must be at most {MaxTitleLength} characters.");
        if (body.Length > MaxBodyLength)
            throw new ArgumentException($"Body must be at most {MaxBodyLength} characters.");
        return (title, body);
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var lower = text.ToLowerInvariant();
        lower = NonWord().Replace(lower, " ");
        return string.Join(' ', lower.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 1 && !StopWords.Contains(w))
            .Select(CanonicalToken));
    }

    public static HashSet<string> TokenSet(string title, string body)
    {
        var normalized = Normalize($"{title} {body}");
        if (normalized.Length == 0)
            return [];
        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    internal static string CanonicalToken(string word)
    {
        if (word.Length < 4)
            return word;

        if (word.EndsWith("ies", StringComparison.Ordinal) && word.Length > 4)
            return word[..^3] + "y";

        if (word.EndsWith("ing", StringComparison.Ordinal) && word.Length > 5)
            return word[..^3];

        if (word.EndsWith("es", StringComparison.Ordinal) && word.Length > 4)
            return word[..^2];

        if (word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
            return word[..^1];

        return word;
    }

    public static double Similarity(string titleA, string bodyA, string titleB, string bodyB)
    {
        var a = TokenSet(titleA, bodyA);
        var b = TokenSet(titleB, bodyB);
        if (a.Count == 0 && b.Count == 0) return 1.0;
        if (a.Count == 0 || b.Count == 0) return 0.0;
        var intersection = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    public static string PickSummaryTitle(string existing, string candidate)
    {
        existing = existing.Trim();
        candidate = candidate.Trim();
        if (candidate.Length == 0) return TruncateTitle(existing);
        if (existing.Length == 0) return TruncateTitle(candidate);
        return candidate.Length < existing.Length ? TruncateTitle(candidate) : TruncateTitle(existing);
    }

    public static string TruncateTitle(string title)
    {
        title = title.Trim();
        return title.Length <= MaxTitleLength ? title : title[..MaxTitleLength];
    }

    [GeneratedRegex(@"[^a-z0-9\s]", RegexOptions.Compiled)]
    private static partial Regex NonWord();
}

public sealed record ClusterCentroid(string Id, string SummaryTitle, string CentroidTitle, string CentroidBody);
