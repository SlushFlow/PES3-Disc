namespace PES3Disc.Core;

public sealed class DiscOverlayMaterializer
{
    private readonly OverlayPolicy _policy;

    public DiscOverlayMaterializer(OverlayPolicy policy) => _policy = policy;

    public async Task<DiscOverlayResult> BuildAsync(
        string discGameRoot,
        string sessionRoot,
        IProgress<StageProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        discGameRoot = Path.GetFullPath(discGameRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        sessionRoot = Path.GetFullPath(sessionRoot);
        Directory.CreateDirectory(sessionRoot);

        var localBudget = _policy.MaxLocalMegabytes * 1024L * 1024L;
        long localUsed = 0;
        var localFiles = 0;
        var linkedFiles = 0;
        long linkedBytes = 0;
        var linkFailures = 0;

        var allFiles = Directory.EnumerateFiles(discGameRoot, "*", SearchOption.AllDirectories).ToList();
        var total = allFiles.Count;
        var processed = 0;

        foreach (var sourceFile in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            var rel = Path.GetRelativePath(discGameRoot, sourceFile);
            var destFile = Path.Combine(sessionRoot, rel);

            progress?.Report(new StageProgress
            {
                Status = "Preparing disc-assisted session…",
                FilesCopied = processed,
                TotalFiles = total,
            });

            if (ShouldCopyLocally(rel, sourceFile, localUsed, localBudget))
            {
                var dir = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.Copy(sourceFile, destFile, overwrite: true);
                var len = new FileInfo(sourceFile).Length;
                localUsed += len;
                localFiles++;
                continue;
            }

            var dirParent = Path.GetDirectoryName(destFile);
            if (!string.IsNullOrEmpty(dirParent))
                Directory.CreateDirectory(dirParent);

            if (FilesystemLink.TryCreateFileLink(destFile, sourceFile))
            {
                linkedFiles++;
                try { linkedBytes += new FileInfo(sourceFile).Length; } catch { /* ignore */ }
            }
            else
            {
                linkFailures++;
                File.Copy(sourceFile, destFile, overwrite: true);
                localUsed += new FileInfo(sourceFile).Length;
                localFiles++;
            }
        }

        if (linkFailures > 0 && linkedFiles == 0 && allFiles.Count > 8)
        {
            Pes3Log.Write($"Disc overlay: {linkFailures} link failures; falling back to full copy.");
            try
            {
                if (Directory.Exists(sessionRoot))
                    Directory.Delete(sessionRoot, recursive: true);
            }
            catch
            {
                // ignore
            }

            Directory.CreateDirectory(sessionRoot);
            var ok = await DirectoryStaging.CopyTreeAsync(discGameRoot, sessionRoot, progress, cancellationToken)
                .ConfigureAwait(false);
            if (!ok)
                throw new InvalidOperationException("Disc overlay fallback copy failed.");

            return new DiscOverlayResult
            {
                SessionRoot = sessionRoot,
                EbootPath = ResolveEboot(sessionRoot),
                Stats = new OverlayStats
                {
                    LocalBytes = DirectoryStaging.CountFiles(sessionRoot) > 0
                        ? allFiles.Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } })
                        : 0,
                    LocalFiles = allFiles.Count,
                    UsedFullCopyFallback = true,
                },
            };
        }

        return new DiscOverlayResult
        {
            SessionRoot = sessionRoot,
            EbootPath = ResolveEboot(sessionRoot),
            Stats = new OverlayStats
            {
                LocalBytes = localUsed,
                LocalFiles = localFiles,
                LinkedFiles = linkedFiles,
                LinkedBytes = linkedBytes,
                UsedFullCopyFallback = false,
            },
        };
    }

    private bool ShouldCopyLocally(string relativePath, string sourceFile, long localUsed, long localBudget)
    {
        var norm = relativePath.Replace('\\', '/');
        foreach (var pattern in _policy.AlwaysLocalPatterns)
        {
            if (norm.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                || norm.EndsWith('/' + pattern, StringComparison.OrdinalIgnoreCase)
                || norm.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        try
        {
            var len = new FileInfo(sourceFile).Length;
            if (len <= _policy.MaxLocalFileBytes && localUsed + len <= localBudget)
                return true;
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static string ResolveEboot(string sessionRoot)
    {
        var eboot = Path.Combine(sessionRoot, "PS3_GAME", "USRDIR", "EBOOT.BIN");
        if (File.Exists(eboot))
            return eboot;

        var found = DiscDetector.FindGameOnDrive(sessionRoot + Path.DirectorySeparatorChar);
        if (found is null)
            throw new InvalidOperationException("EBOOT.BIN not found in disc-assisted session.");
        return found.EbootPath;
    }
}

public sealed class DiscOverlayResult
{
    public required string SessionRoot { get; init; }
    public required string EbootPath { get; init; }
    public OverlayStats? Stats { get; init; }
}
