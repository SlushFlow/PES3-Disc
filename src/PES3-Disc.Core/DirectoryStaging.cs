using System.Diagnostics;

namespace PES3Disc.Core;

public static class DirectoryStaging
{
    public static int CountFiles(string root)
    {
        try
        {
            return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count();
        }
        catch
        {
            return 0;
        }
    }

    public static async Task<bool> CopyTreeAsync(
        string source,
        string destination,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(source))
            return false;

        Directory.CreateDirectory(destination);
        var total = CountFiles(source);
        var copied = 0;

        if (TryRobocopy(source, destination, out var robocopyOk) && robocopyOk)
        {
            progress?.Report(new StageProgress
            {
                Status = "Copy complete",
                FilesCopied = total,
                TotalFiles = total,
            });
            return true;
        }

        return await CopyTreeManagedAsync(source, destination, total, copied, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool TryRobocopy(string source, string destination, out bool success)
    {
        success = false;
        try
        {
            var robocopy = FindRobocopy();
            if (robocopy is null)
                return false;

            var psi = new ProcessStartInfo
            {
                FileName = robocopy,
                Arguments = $"\"{source}\" \"{destination}\" /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NJH /NJS /nc /ns /np",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;
            proc.WaitForExit();
            success = proc.ExitCode is >= 0 and <= 7;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindRobocopy()
    {
        var system32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "robocopy.exe");
        return File.Exists(system32) ? system32 : null;
    }

    private static async Task<bool> CopyTreeManagedAsync(
        string source,
        string destination,
        int total,
        int copied,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(source, dir);
                Directory.CreateDirectory(Path.Combine(destination, rel));
            }

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(source, file);
                var destFile = Path.Combine(destination, rel);
                var destDir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);
                File.Copy(file, destFile, overwrite: true);
                copied++;
                if (copied % 25 == 0 || copied == total)
                {
                    progress?.Report(new StageProgress
                    {
                        Status = Path.GetFileName(file),
                        FilesCopied = copied,
                        TotalFiles = total,
                    });
                }
            }

            return copied > 0 || total == 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Copy failed ({source}): {ex.Message}");
            return false;
        }
    }
}
