using System.Reflection;
using PES3Disc.BugReports;
using PES3Disc.Core;

namespace PES3Disc.ViewModels;

public interface IPes3UiHost
{
    Task<bool> ConfirmLegalTermsAsync();
    Task<PlaySession?> ShowStageDialogAsync(OpticalDrive drive, DetectedGame game, Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> work);
    Task<DecryptResult?> ShowDecryptDialogAsync(OpticalDrive drive, string outputDir, Func<IProgress<DecryptProgress>, CancellationToken, Task<DecryptResult>> work);
    void ShowWarning(string message);
    void ShowInfo(string message);
}

public sealed class Pes3AppController
{
    private readonly Pes3Services _svc;
    private readonly HashSet<string> _prompted;
    private string? _lastScanSignature;
    private List<DiscCardModel>? _lastScanCards;

    public Pes3AppController(Pes3Services services)
    {
        _svc = services;
        _prompted = services.Prompted.Load();
    }

    public Pes3Services Services => _svc;

    public string? ConfigLoadWarning => _svc.ConfigLoadWarning;

    public string Subtitle =>
        $"RPCS3: {Path.GetFileName(_svc.Config.Rpcs3Path)}  •  PES3: {_svc.Paths.Pes3Root ?? "(configure RPCS3)"}";

    public string Footer
    {
        get
        {
            var mode = Pes3StorageModeResolver.Resolve(_svc.Config);
            return mode switch
            {
                Pes3StorageMode.EphemeralSession =>
                    "Session storage: temp data removed when RPCS3 exits (saves disk, slow repeat).",
                Pes3StorageMode.DiscDirect =>
                    "Disc storage: DIY plays from the disc with no copy (keep disc inserted).",
                Pes3StorageMode.PersistentLibrary =>
                    $"Library: {_svc.Paths.LibraryRoot} — full titles kept for instant replay.",
                _ =>
                    $"Smart: DIY disc-assisted sessions; retail decrypts once into {_svc.Paths.LibraryRoot} for instant replay.",
            };
        }
    }

    public void SavePrompted() => _svc.Prompted.Save(_prompted);

    public void Dismiss(OpticalDrive drive)
    {
        _prompted.Add(drive.Id);
        SavePrompted();
    }

    public void InvalidateScanCache()
    {
        _lastScanSignature = null;
        _lastScanCards = null;
    }

    public List<DiscCardModel> ScanVolumes(bool forceRefresh = false)
    {
        var signature = VolumeScanCache.ComputeSignature();
        if (!forceRefresh && signature == _lastScanSignature && _lastScanCards is not null)
            return _lastScanCards;

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
            var productCode = GameMetadata.TryReadProductCodeFromVolume(drive.Root);
            var cached = _svc.Cache.TryGetCached(drive.Id, status.Game?.TitleId, productCode);
            CachedGameEntry? retailCache = null;

            if (status.Kind == DiscVolumeKind.Playable && status.Game is not null)
            {
                detail = cached is not null
                    ? "Library hit — instant play from SSD."
                    : Pes3StorageModeResolver.Resolve(_svc.Config) == Pes3StorageMode.SmartHybrid
                        ? "Disc-assisted: small SSD session, bulk read from disc."
                        : Pes3StorageModeResolver.AllowsDiscDirect(_svc.Config)
                            ? "Play from disc (no copy) or ephemeral session."
                            : "Will stage into the PES3 library for fast RPCS3 loading.";
            }
            else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn)
            {
                retailCache = _svc.Cache.TryGetCached(drive.Id, null, productCode);
                if (retailCache is not null)
                    detail = "Title in library — Play from library (no re-decrypt).";
                else if (Pes3StorageModeResolver.PromotesRetailToLibrary(_svc.Config))
                    detail = "First decrypt takes 30–90+ min; then instant replay from your library (no piracy, no manual dumps).";
                else
                    detail = "Decrypt once per session (~30–90+ min); session removed when RPCS3 exits or disc ejects.";
            }

            detail += $"  ({drive.Root})";

            cards.Add(new DiscCardModel
            {
                Drive = drive,
                Status = status,
                Title = title,
                Detail = detail,
                IsDismissed = _prompted.Contains(drive.Id),
                CanPlay = status.Kind == DiscVolumeKind.Playable && status.Game is not null,
                CanPlayFromCache = retailCache is not null,
                LibraryEntry = retailCache,
                PlayButtonText = cached is not null ? "Play from library" : "Play",
                CanDecrypt = status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn
                    && _svc.Config.EnableRetailDecrypt,
                CanDecryptAgain = retailCache is not null,
                DecryptAvailable = _svc.Decryptor.IsAvailable,
            });
        }

        _lastScanSignature = signature;
        _lastScanCards = cards;
        return cards;
    }

    public async Task<string?> PlayGameAsync(OpticalDrive drive, DetectedGame game, IPes3UiHost ui, CancellationToken ct = default)
    {
        _prompted.Add(drive.Id);
        SavePrompted();

        PlaySession session;
        var cached = _svc.Cache.TryGetCached(drive.Id, game.TitleId, null);
        if (cached is null && !await EnsureLegalTermsAsync(ui, ct).ConfigureAwait(false))
            return null;

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
        if (!await EnsureLegalTermsAsync(ui, ct).ConfigureAwait(false))
            return null;

        var session = _svc.Cache.SessionFromCached(cached);
        return await LaunchSessionAsync(session, ui, ct);
    }

    public async Task<string?> DecryptAndPlayAsync(OpticalDrive drive, IPes3UiHost ui, CancellationToken ct = default)
    {
        if (!await EnsureLegalTermsAsync(ui, ct).ConfigureAwait(false))
            return null;

        if (!_svc.Decryptor.IsAvailable)
        {
            ui.ShowWarning(OperatingSystem.IsLinux()
                ? "pes3-disc-dump-linux is missing. Reinstall the Linux package or set DumpCliPath."
                : "pes3-disc-dump.exe is missing. Re-run Build-App.ps1 with .NET 10 SDK.");
            return null;
        }

        _prompted.Add(drive.Id);
        SavePrompted();

        var cached = await _svc.Cache.TryGetRetailCachedAsync(
            drive,
            token => _svc.Decryptor.ProbeDiscAsync(drive, token),
            ct).ConfigureAwait(false);
        if (cached is not null)
            return await PlayFromCacheAsync(cached, ui, ct);

        DiscProbeResult? probe = null;
        try
        {
            probe = await _svc.Decryptor.ProbeDiscAsync(drive, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Pes3Log.Write($"Disc probe before decrypt: {ex.Message}");
        }

        var mode = Pes3StorageModeResolver.Resolve(_svc.Config);
        var outputDir = _svc.Cache.ResolveRetailOutputDir(probe?.ProductCode);
        Directory.CreateDirectory(outputDir);
        var ephemeralOutput = mode is Pes3StorageMode.EphemeralSession or Pes3StorageMode.SmartHybrid;
        var cleanup = ephemeralOutput ? new List<string> { outputDir } : new List<string>();

        var result = await ui.ShowDecryptDialogAsync(
            drive,
            outputDir,
            (progress, token) => _svc.Decryptor.DecryptAsync(drive, outputDir, progress, token));

        if (result is not { Success: true })
        {
            if (ephemeralOutput && Directory.Exists(outputDir))
            {
                try { Directory.Delete(outputDir, true); } catch { /* ignore */ }
            }
            if (result?.ErrorMessage is { } err)
                ui.ShowWarning(err);
            return null;
        }

        InvalidateScanCache();
        var session = _svc.Cache.FinalizeRetailDecrypt(result, outputDir, cleanup);
        session = new PlaySession
        {
            EbootPath = session.EbootPath,
            CleanupDirs = session.CleanupDirs,
            FromCache = session.FromCache,
            CacheDir = session.CacheDir,
            Tier = session.Tier,
            VolumeId = drive.Id,
            DiscRoot = drive.Root,
            OverlayStats = session.OverlayStats,
        };
        return await LaunchSessionAsync(session, ui, ct);
    }

    public void CleanupEjectedVolumes()
    {
        if (!_svc.Config.CleanupSessionsOnDiscEject)
            return;
        _svc.SessionRegistry.CleanupEjectedVolumes();
        InvalidateScanCache();
    }

    private async Task<bool> EnsureLegalTermsAsync(IPes3UiHost ui, CancellationToken ct)
    {
        if (LegalTerms.IsAccepted(_svc.Config))
            return true;

        if (!await ui.ConfirmLegalTermsAsync().ConfigureAwait(false))
            return false;

        LegalTerms.RecordAcceptance(_svc.Config);
        _svc.SaveConfig();
        Pes3Log.Write($"Legal terms accepted ({LegalTerms.CurrentVersion}).");
        return true;
    }

    private async Task<string?> LaunchSessionAsync(PlaySession session, IPes3UiHost ui, CancellationToken ct)
    {
        var launchError = _svc.Launcher.DescribeLaunchFailure(session.EbootPath);
        if (launchError is not null)
        {
            ui.ShowWarning(launchError);
            return null;
        }

        var proc = await _svc.Launcher.LaunchSessionAsync(session, ct).ConfigureAwait(false);
        if (proc is null)
        {
            ui.ShowWarning(_svc.Launcher.DescribeLaunchFailure(session.EbootPath)
                ?? "Could not start RPCS3. Check Settings.");
            return null;
        }

        return session.EbootPath;
    }

    public async Task<string> SubmitBugReportAsync(string title, string body, string platform, CancellationToken ct = default)
    {
        var apiUrl = string.IsNullOrWhiteSpace(_svc.Config.BugReportApiUrl)
            ? BugReportEndpoints.DefaultApiBaseUrl
            : _svc.Config.BugReportApiUrl.Trim();
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        using var client = new BugReportClient(apiUrl);
        var result = await client.SubmitAsync(new BugReportSubmission
        {
            Title = title,
            Body = body,
            Platform = platform,
            AppVersion = version,
            OsDescription = Environment.OSVersion.ToString(),
        }, ct);
        BugReportPendingTracker.TrackSubmission(result.Id, title);
        return result.Id;
    }

    public string BugReportApiUrl =>
        string.IsNullOrWhiteSpace(_svc.Config.BugReportApiUrl)
            ? BugReportEndpoints.DefaultApiBaseUrl
            : _svc.Config.BugReportApiUrl.Trim();
}
