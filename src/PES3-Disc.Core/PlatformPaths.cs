namespace PES3Disc.Core;

public static class PlatformPaths
{
    public static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PES3-Disc");
            }

            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
                return Path.Combine(xdg, "PES3-Disc");
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config",
                "PES3-Disc");
        }
    }

    public static string DefaultIrdDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ps3-disc-dumper",
                    "ird");
            }

            var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            var baseDir = string.IsNullOrWhiteSpace(xdgData)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local",
                    "share")
                : xdgData;
            return Path.Combine(baseDir, "ps3-disc-dumper", "ird");
        }
    }

    public static IReadOnlyList<string> Rpcs3CandidatePaths()
    {
        if (OperatingSystem.IsWindows())
        {
            return new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RPCS3", "rpcs3.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "RPCS3", "rpcs3.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "RPCS3", "rpcs3.exe"),
            };
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            "/usr/bin/rpcs3",
            "/usr/local/bin/rpcs3",
            Path.Combine(home, ".local", "share", "rpcs3", "rpcs3"),
            Path.Combine(home, "Applications", "rpcs3.AppImage"),
            Path.Combine(home, "bin", "rpcs3"),
        };
    }

    public static string[] DumpCliCandidateNames() =>
        OperatingSystem.IsWindows()
            ? new[] { "pes3-disc-dump.exe", "pes3-disc-dump" }
            : new[] { "pes3-disc-dump", "pes3-disc-dump.exe" };
}
