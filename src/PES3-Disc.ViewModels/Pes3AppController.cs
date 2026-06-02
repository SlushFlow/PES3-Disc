using PES3Disc.Core;

namespace PES3Disc.ViewModels;

public interface IPes3UiHost
{
    Task<PlaySession?> ShowStageDialogAsync(OpticalDrive drive, DetectedGame game, Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> work);
    Task<DecryptResult?> ShowDecryptDialogAsync(OpticalDrive drive, string outputDir, Func<IProgress<DecryptProgress>, CancellationToken, Task<DecryptResult>> work);
    void ShowWarning(string message);
    void ShowInfo(string message);
}

public sealed class Pes3AppController
{
    private readonly Pes3Services _svc;
    private readonly HashSet<string> _prompted;

    public Pes3AppController(Pes3Services services)
    {
        _svc = services;
        _prompted = services.Prompted.Load();
    }

    public Pes3Services Services => _svc;

    public string Subtitle =>
        $"RPCS3: {Path.GetFileName(_svc.Config.Rpcs3Path)}  •  PES3: {_svc.Paths.Pes3Root ?? "(configure RPCS3)"}";

    public string Footer
    {
        get
        {
            var cacheNote = _svc.Config.DeleteCacheAfterPlay
                ? "Session cache (cleared after play)"
                : $"Persistent cache: {_svc.Paths.CacheRoot}";
            return $"DIY and retail discs use the same PES3 cache for fast RPCS3 loading. {cacheNote}.";
        }
    }

    public void SavePrompted() => _svc.Prompted.Save(_prompted);

    public void Dismiss(OpticalDrive drive)
    {
        _prompted.Add(drive.Id);
        SavePrompted();
    }

    public List<DiscCardModel> ScanVolumes()
    {
        var cards = new List<DiscCardModel>();
        foreach (var drive in DiscDetector.GetOpticalDrives())
        {
            var status = DiscDetector.GetVolumeStatus(drive.Root);
            if (status.Kind == DiscVolumeKind.NoPs3Layout && !_svc.Config.DecryptUnknownOpticalMedia)
                continue;

            var title = status.Game?.Title ?? status.Kind switch
            {
                DiscVolumeKind.EncryptedRetail => "Retail PS3 disc",
                DiscVolumeKind.IncompleteBurn => "Incomplete PS3 layout",
                _ => $"Drive {drive.DisplayName}",
            };

            var detail = status.Message;
            var cached = _svc.Cache.TryGetCached(drive.Id, status.Game?.TitleId, null);
            if (status.Kind == DiscVolumeKind.Playable && status.Game is not null)
            {
                detail = cached is not null
                    ? "Cached copy on disk — instant play from SSD."
                    : "Will copy to PES3 cache for the same fast loading as decrypted retail games.";
            }
            else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn)
            {
                var retailCached = _svc.Cache.TryGetCached(drive.Id, null, null);
                if (retailCached is not null)
                    detail = "Decrypted game already in cache — play without re-decrypting.";
            }

            detail += $"  ({drive.Root})";

            var retailCache = _svc.Cache.TryGetCached(drive.Id, null, null);
            cards.Add(new DiscCardModel
            {
                Drive = drive,
                Status = status,
                Title = title,
                Detail = detail,
                IsDismissed = _prompted.Contains(drive.Id),
                CanPlay = status.Kind == DiscVolumeKind.Playable && status.Game is not null,
                CanPlayFromCache = retailCache is not null,
                PlayButtonText = cached is not null ? "Play from cache" : "Play",
                CanDecrypt = status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn
                    && _svc.Config.EnableRetailDecrypt,
                CanDecryptAgain = retailCache is not null,
                DecryptAvailable = _svc.Decryptor.IsAvailable,
            });
        }
        return cards;
    }

    public async Task<string?> PlayGameAsync(OpticalDrive drive, DetectedGame game, IPes3UiHost ui, CancellationToken ct = default)
    {
        _prompted.Add(drive.Id);
        SavePrompted();

        PlaySession session;
        var cached = _svc.Cache.TryGetCached(drive.Id, game.TitleId, null);
        if (cached is not null)
        {
            session = _svc.Cache.SessionFromCached(cached);
        }
        else
        {
            var staged = await ui.ShowStageDialogAsync(
                drive,
                game,
                (progress, token) => _svc.Cache.PrepareDiyPlayAsync(drive, game, progress, token));
            if (staged is null)
                return null;
            session = staged;
        }

        return await LaunchSessionAsync(session, ui, ct);
    }

    public async Task<string?> PlayFromCacheAsync(CachedGameEntry cached, IPes3UiHost ui, CancellationToken ct = default)
    {
        var session = _svc.Cache.SessionFromCached(cached);
        return await LaunchSessionAsync(session, ui, ct);
    }

    public async Task<string?> DecryptAndPlayAsync(OpticalDrive drive, IPes3UiHost ui, CancellationToken ct = default)
    {
        if (!_svc.Decryptor.IsAvailable)
        {
            ui.ShowWarning(OperatingSystem.IsLinux()
                ? "pes3-disc-dump-linux is missing. Reinstall the Linux package or set DumpCliPath."
                : "pes3-disc-dump.exe is missing. Re-run Build-App.ps1 with .NET 10 SDK.");
            return null;
        }

        _prompted.Add(drive.Id);
        SavePrompted();

        var cached = _svc.Cache.TryGetCached(drive.Id, null, null);
        if (cached is not null)
            return await PlayFromCacheAsync(cached, ui, ct);

        string outputDir;
        List<string> cleanup;
        if (_svc.Config.DeleteCacheAfterPlay)
        {
            outputDir = _svc.Paths.NewSessionDir();
            cleanup = new List<string> { outputDir };
        }
        else
        {
            outputDir = _svc.Cache.ResolveRetailOutputDir(null);
            Directory.CreateDirectory(outputDir);
            cleanup = new List<string>();
        }

        var result = await ui.ShowDecryptDialogAsync(
            drive,
            outputDir,
            (progress, token) => _svc.Decryptor.DecryptAsync(drive, outputDir, progress, token));

        if (result is not { Success: true })
        {
            if (_svc.Config.DeleteCacheAfterPlay && Directory.Exists(outputDir))
            {
                try { Directory.Delete(outputDir, true); } catch { /* ignore */ }
            }
            if (result?.ErrorMessage is { } err)
                ui.ShowWarning(err);
            return null;
        }

        var session = _svc.Cache.FinalizeRetailDecrypt(result, outputDir, cleanup);
        return await LaunchSessionAsync(session, ui, ct);
    }

    private async Task<string?> LaunchSessionAsync(PlaySession session, IPes3UiHost ui, CancellationToken ct)
    {
        var proc = await _svc.Launcher.LaunchGameAsync(session.EbootPath, session.CleanupDirs.ToList(), ct);
        if (proc is null)
        {
            ui.ShowWarning("Could not start RPCS3. Check Settings.");
            return null;
        }
        return session.EbootPath;
    }
}
