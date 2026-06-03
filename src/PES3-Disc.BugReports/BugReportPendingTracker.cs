using System.Text.Json;

namespace PES3Disc.BugReports;

public sealed class BugReportPendingEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime SubmittedAtUtc { get; set; }
    public bool Notified { get; set; }
}

public static class BugReportPendingTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string GetStorePath()
    {
        string dir;
        if (OperatingSystem.IsWindows())
        {
            dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PES3-Disc");
        }
        else
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            dir = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "PES3-Disc")
                : Path.Combine(xdg, "PES3-Disc");
        }

        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "pending-bug-reports.json");
    }

    public static List<BugReportPendingEntry> Load()
    {
        var path = GetStorePath();
        if (!File.Exists(path))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<BugReportPendingEntry>>(File.ReadAllText(path), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<BugReportPendingEntry> entries)
    {
        var path = GetStorePath();
        File.WriteAllText(path, JsonSerializer.Serialize(entries.ToList(), JsonOptions));
    }

    public static void TrackSubmission(string reportId, string title)
    {
        var list = Load();
        if (list.Any(e => e.Id == reportId))
            return;

        list.Add(new BugReportPendingEntry
        {
            Id = reportId,
            Title = title.Trim(),
            SubmittedAtUtc = DateTime.UtcNow,
        });
        Save(list);
    }

    public static void MarkNotified(string reportId)
    {
        var list = Load();
        var next = list.Where(e => e.Id != reportId).ToList();
        if (next.Count != list.Count)
            Save(next);
    }
}
