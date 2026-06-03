namespace PES3Disc.Core;

/// <summary>Windows retail decrypt via pes3-disc-dump.exe (PS3 Disc Dumper engine).</summary>
public sealed class WindowsDiscDumpBackend : DiscDumpBackendBase
{
    public WindowsDiscDumpBackend(Pes3Config? config = null) : base(config) { }

    public override bool IsAvailable => FindDumpCliPath() is not null;

    protected override string? FindDumpCliPath()
    {
        if (Config is not null && !string.IsNullOrWhiteSpace(Config.DumpCliPath) && File.Exists(Config.DumpCliPath))
            return Config.DumpCliPath;

        var baseDir = AppContext.BaseDirectory;
        foreach (var name in PlatformPaths.DumpCliCandidateNames())
        {
            foreach (var path in new[]
            {
                Path.Combine(baseDir, name),
                Path.Combine(baseDir, "tools", name),
                Path.GetFullPath(Path.Combine(baseDir, "..", "tools", name)),
            })
            {
                if (File.Exists(path))
                    return path;
            }
        }
        return null;
    }

    protected override string BuildArguments(OpticalDrive drive, string outputBase, string progressFile) =>
        AppendDriveArgs($"--output \"{outputBase}\" --progress \"{progressFile}\"", drive);

    protected override string BuildProbeArguments(OpticalDrive drive) =>
        AppendDriveArgs("--probe", drive);

    private string AppendDriveArgs(string prefix, OpticalDrive drive)
    {
        var args = prefix;
        if (OperatingSystem.IsWindows())
            args += $" --drive {drive.Letter}";
        else if (!string.IsNullOrWhiteSpace(drive.Root))
            args += $" --mount \"{drive.Root.TrimEnd('/', '\\')}\"";
        if (Config is not null && !string.IsNullOrWhiteSpace(Config.IrdDir))
            args += $" --ird-dir \"{Config.IrdDir.Trim()}\"";
        else
            args += $" --ird-dir \"{PlatformPaths.DefaultIrdDirectory}\"";
        if (!string.IsNullOrWhiteSpace(drive.DeviceNode))
            args += $" --device \"{drive.DeviceNode}\"";
        return args;
    }
}
