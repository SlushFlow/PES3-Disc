namespace PES3Disc.Core;

public static class SessionCleanup
{
    public static void DeleteTrees(IEnumerable<string> dirs)
    {
        foreach (var dir in dirs.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(d => d.Length))
        {
            DeleteTree(dir);
        }
    }

    public static void DeleteTree(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return;

        try
        {
            Directory.Delete(dir, recursive: true);
            Pes3Log.Write($"Session cleanup removed: {dir}");
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Session cleanup failed for {dir}: {ex.Message}");
        }
    }
}
