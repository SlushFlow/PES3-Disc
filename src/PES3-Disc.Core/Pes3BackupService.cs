using System.Text.Json;

namespace PES3Disc.Core;

public sealed class Pes3BackupService
{
    private readonly Pes3Config _config;
    private readonly Pes3Paths _paths;

    public Pes3BackupService(Pes3Config config, Pes3Paths paths)
    {
        _config = config;
        _paths = paths;
    }

    public bool IsEnabled => _config.EnableBackups;

    public async Task<string?> BackupBeforePlayAsync(
        string ebootPath,
        IReadOnlyList<string> sourceDirs,
        string reason = "before_play",
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
            return null;

        var shouldRun = sourceDirs.Count > 0 || _config.BackupOnLaunch;
        if (!shouldRun)
            return null;

        var (titleId, title) = GameMetadata.ReadTitleFromEboot(ebootPath);
        if (RecentBackupExists(titleId, reason))
            return null;

        var roots = ResolveRoots(ebootPath, sourceDirs);
        if (roots.Count == 0)
            return null;

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backupDir = Path.Combine(_paths.BackupRoot, titleId, stamp);
        var gameDir = Path.Combine(backupDir, "game");
        Directory.CreateDirectory(gameDir);

        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dest = Path.Combine(gameDir, Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar)));
            await DirectoryStaging.CopyTreeAsync(root, dest, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        var manifest = new
        {
            created = DateTime.UtcNow.ToString("o"),
            reason,
            titleId,
            title,
            ebootPath,
            sourceRoots = roots,
        };
        await File.WriteAllTextAsync(
            Path.Combine(backupDir, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken).ConfigureAwait(false);

        PruneOldBackups(titleId);
        Pes3Log.Write($"Backup created: {backupDir}");
        return backupDir;
    }

    private List<string> ResolveRoots(string ebootPath, IReadOnlyList<string> sourceDirs)
    {
        var roots = new List<string>();
        foreach (var dir in sourceDirs.Distinct())
        {
            if (!Directory.Exists(dir))
                continue;
            if (Directory.Exists(Path.Combine(dir, "PS3_GAME")))
                roots.Add(dir);
            else if (Path.GetFileName(dir) == "PS3_GAME")
                roots.Add(Directory.GetParent(dir)?.FullName ?? dir);
            else
                roots.Add(dir);
        }

        var gameRoot = GameMetadata.GetGameRootFromEboot(ebootPath);
        if (gameRoot is not null && Directory.Exists(gameRoot) && !roots.Contains(gameRoot, StringComparer.OrdinalIgnoreCase))
            roots.Insert(0, gameRoot);

        return roots;
    }

    private bool RecentBackupExists(string titleId, string reason, int windowSeconds = 120)
    {
        var titleDir = Path.Combine(_paths.BackupRoot, titleId);
        if (!Directory.Exists(titleDir))
            return false;

        var latest = Directory.EnumerateDirectories(titleDir)
            .OrderByDescending(d => d)
            .FirstOrDefault();
        if (latest is null)
            return false;

        var manifestPath = Path.Combine(latest, "manifest.json");
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!doc.RootElement.TryGetProperty("reason", out var r) || r.GetString() != reason)
                return false;
            if (!doc.RootElement.TryGetProperty("created", out var c))
                return false;
            var created = DateTime.Parse(c.GetString()!);
            return (DateTime.UtcNow - created.ToUniversalTime()).TotalSeconds < windowSeconds;
        }
        catch
        {
            return false;
        }
    }

    private void PruneOldBackups(string titleId)
    {
        var titleDir = Path.Combine(_paths.BackupRoot, titleId);
        if (!Directory.Exists(titleDir))
            return;

        var keep = Math.Max(1, _config.MaxBackupsPerTitle);
        foreach (var old in Directory.EnumerateDirectories(titleDir).OrderByDescending(d => d).Skip(keep))
        {
            try { Directory.Delete(old, recursive: true); } catch { /* ignore */ }
        }
    }
}
