namespace PES3Disc.Core;

public static class Pes3Log
{
    private static readonly object Gate = new();
    private static string _path = Path.Combine(AppContext.BaseDirectory, "disc-run.log");

    public static void SetPath(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        lock (Gate)
        {
            try
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
            catch
            {
                // ignore
            }
        }
    }
}
