using System.Diagnostics;

namespace PES3Disc.Core;

public sealed class Rpcs3Launcher
{
    private readonly Pes3Config _config;
    private readonly Pes3Paths _paths;
    private readonly Pes3BackupService? _backup;

    public Rpcs3Launcher(Pes3Config config, Pes3Paths paths, Pes3BackupService? backup = null)
    {
        _config = config;
        _paths = paths;
        _backup = backup;
    }

    public string? FindRpcs3()
    {
        if (_config.IsRpcs3Configured)
            return _config.Rpcs3Path;

        return PlatformPaths.Rpcs3CandidatePaths().FirstOrDefault(File.Exists);
    }

    public async Task<Process?> LaunchGameAsync(
        string ebootPath,
        IReadOnlyList<string>? cleanupDirs = null,
        CancellationToken cancellationToken = default)
    {
        if (_backup is not null)
        {
            await _backup.BackupBeforePlayAsync(
                ebootPath,
                cleanupDirs ?? Array.Empty<string>(),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return LaunchGame(ebootPath, cleanupDirs);
    }

    public Process? LaunchGame(string ebootPath, IReadOnlyList<string>? cleanupDirs = null)
    {
        var rpcs3 = FindRpcs3();
        if (rpcs3 is null)
            return null;

        var args = _config.UseNoGui ? $"--no-gui \"{ebootPath}\"" : $"\"{ebootPath}\"";
        var psi = new ProcessStartInfo
        {
            FileName = rpcs3,
            Arguments = args,
            WorkingDirectory = Path.GetDirectoryName(rpcs3) ?? "",
            UseShellExecute = false,
        };

        var proc = Process.Start(psi);
        if (proc is not null && cleanupDirs is { Count: > 0 })
            _ = ScheduleCleanupAsync(proc, cleanupDirs);

        Pes3Log.Write($"Launched RPCS3: {rpcs3} {args}");
        return proc;
    }

    private static async Task ScheduleCleanupAsync(Process proc, IReadOnlyList<string> dirs)
    {
        try
        {
            await proc.WaitForExitAsync().ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromSeconds(6)).ConfigureAwait(false);
            SessionCleanup.DeleteTrees(dirs);
        }
        catch
        {
            // ignore
        }
    }
}
