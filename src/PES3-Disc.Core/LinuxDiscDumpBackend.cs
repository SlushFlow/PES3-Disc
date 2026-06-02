namespace PES3Disc.Core;

/// <summary>Linux retail decrypt via pes3-disc-dump-linux (separate PES3-Disc Linux dump tool).</summary>
public sealed class LinuxDiscDumpBackend : DiscDumpBackendBase
{
    public LinuxDiscDumpBackend(Pes3Config? config = null) : base(config) { }

    public override bool IsAvailable => FindDumpCliPath() is not null;

    protected override string? FindDumpCliPath()
    {
        if (Config is not null && !string.IsNullOrWhiteSpace(Config.DumpCliPath) && File.Exists(Config.DumpCliPath))
            return Config.DumpCliPath;

        var baseDir = AppContext.BaseDirectory;
        foreach (var name in new[] { "pes3-disc-dump-linux", "pes3-disc-dump" })
        {
            foreach (var path in new[]
            {
                Path.Combine(baseDir, name),
                Path.Combine(baseDir, "tools", name),
                Path.GetFullPath(Path.Combine(baseDir, "..", name)),
            })
            {
                if (File.Exists(path))
                    return path;
            }
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir.Trim(), "pes3-disc-dump-linux");
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    protected override string BuildArguments(OpticalDrive drive, string outputBase, string progressFile)
    {
        var args = $"--output \"{outputBase}\" --progress \"{progressFile}\"";
        var mount = drive.Root.TrimEnd('/', '\\');
        if (!string.IsNullOrEmpty(mount))
            args += $" --mount \"{mount}\"";
        if (!string.IsNullOrWhiteSpace(drive.DeviceNode))
            args += $" --device \"{drive.DeviceNode}\"";
        if (Config is not null && !string.IsNullOrWhiteSpace(Config.IrdDir))
            args += $" --ird-dir \"{Config.IrdDir.Trim()}\"";
        else
            args += $" --ird-dir \"{PlatformPaths.DefaultIrdDirectory}\"";
        return args;
    }
}
