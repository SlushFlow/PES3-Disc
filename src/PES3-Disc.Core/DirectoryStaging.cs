using System.Buffers;
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
        CancellationToken cancellationToken = default,
        bool fastCopy = true)
    {
        if (!Directory.Exists(source))
            return false;

        Directory.CreateDirectory(destination);
        var total = CountFiles(source);

        if (OperatingSystem.IsWindows() && fastCopy && TryRobocopy(source, destination, out var robocopyOk) && robocopyOk)
        {
            progress?.Report(new StageProgress
            {
                Status = "Copy complete",
                FilesCopied = total,
                TotalFiles = total,
            });
            return true;
        }

        if (OperatingSystem.IsLinux() && fastCopy && TryRsync(source, destination, out var rsyncOk) && rsyncOk)
        {
            progress?.Report(new StageProgress
            {
                Status = "Copy complete",
                FilesCopied = total,
                TotalFiles = total,
            });
            return true;
        }

        return await CopyTreeParallelAsync(source, destination, total, progress, cancellationToken)
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

            var threads = Math.Clamp(PerformanceTuning.RobocopyThreads, 1, 32);
            var psi = new ProcessStartInfo
            {
                FileName = robocopy,
                Arguments = $"\"{source}\" \"{destination}\" /E /COPY:DAT /MT:{threads} /J /R:1 /W:1 /NFL /NDL /NJH /NJS /nc /ns /np",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;
            PerformanceTuning.TryBoostChildProcess(proc);
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

    private static bool TryRsync(string source, string destination, out bool success)
    {
        success = false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "rsync",
                Arguments = $"-a --whole-file --info=progress2 \"{source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}/\" \"{destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}/\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;
            proc.WaitForExit();
            success = proc.ExitCode == 0;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CopyTreeParallelAsync(
        string source,
        string destination,
        int total,
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

            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToList();
            var copied = 0;
            var parallel = Math.Clamp(Environment.ProcessorCount, 2, 8);
            var gate = new object();

            await Parallel.ForEachAsync(
                files,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallel,
                    CancellationToken = cancellationToken,
                },
                async (file, ct) =>
                {
                    var rel = Path.GetRelativePath(source, file);
                    var destFile = Path.Combine(destination, rel);
                    var destDir = Path.GetDirectoryName(destFile);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    await CopyFileBufferedAsync(file, destFile, ct).ConfigureAwait(false);

                    var n = Interlocked.Increment(ref copied);
                    if (n % 25 == 0 || n == total)
                    {
                        lock (gate)
                        {
                            progress?.Report(new StageProgress
                            {
                                Status = Path.GetFileName(file),
                                FilesCopied = n,
                                TotalFiles = total,
                            });
                        }
                    }
                }).ConfigureAwait(false);

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

    private static async Task CopyFileBufferedAsync(string source, string dest, CancellationToken cancellationToken)
    {
        await using var src = new FileStream(
            source,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            PerformanceTuning.FileCopyBufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var dst = new FileStream(
            dest,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            PerformanceTuning.FileCopyBufferBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = ArrayPool<byte>.Shared.Rent(PerformanceTuning.FileCopyBufferBytes);
        try
        {
            int read;
            while ((read = await src.ReadAsync(buffer.AsMemory(0, PerformanceTuning.FileCopyBufferBytes), cancellationToken)
                         .ConfigureAwait(false)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
