using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PES3Disc.BugReports;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _scanTimer;
    private readonly DispatcherTimer _bugReportTimer;
    private readonly DevStatusTracker _devStatusTracker;
    private readonly BugReportResolutionPoller _bugReportPoller = new();
    private readonly HashSet<string> _prompted;
    private bool _scanInProgress;
    private bool _bugReportPollInProgress;

    public MainWindow()
    {
        InitializeComponent();
        _prompted = App.Services.Prompted.Load();
        RefreshHeader();
        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(2, App.Services.Config.ScanDelaySeconds)),
        };
        _scanTimer.Tick += async (_, _) => await RunScanAsync();
        _scanTimer.Start();
        _bugReportTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _bugReportTimer.Tick += async (_, _) => await PollBugReportResolutionsAsync();
        _bugReportTimer.Start();
        var devStatusApiUrl = string.IsNullOrWhiteSpace(App.Services.Config.BugReportApiUrl)
            ? BugReportEndpoints.DefaultApiBaseUrl
            : App.Services.Config.BugReportApiUrl.Trim();
        _devStatusTracker = new DevStatusTracker(devStatusApiUrl);
        _devStatusTracker.Changed += status => Dispatcher.Invoke(() => DevStatus.Apply(status));
        _devStatusTracker.Start(TimeSpan.FromSeconds(20));
        Closed += (_, _) => _devStatusTracker.Dispose();
        Loaded += async (_, _) =>
        {
            await RunScanAsync();
            await PollBugReportResolutionsAsync();
        };
    }

    private async Task PollBugReportResolutionsAsync()
    {
        if (_bugReportPollInProgress)
            return;
        _bugReportPollInProgress = true;
        try
        {
            var apiUrl = string.IsNullOrWhiteSpace(App.Services.Config.BugReportApiUrl)
                ? BugReportEndpoints.DefaultApiBaseUrl
                : App.Services.Config.BugReportApiUrl.Trim();
            var notifications = await _bugReportPoller.PollAsync(apiUrl);
            foreach (var note in notifications)
            {
                MessageBox.Show(this, note.FormatForUser(), "Bug report update", MessageBoxButton.OK, MessageBoxImage.Information);
                BugReportPendingTracker.MarkNotified(note.ReportId);
            }
        }
        catch
        {
            // API unreachable — ignore until next poll
        }
        finally
        {
            _bugReportPollInProgress = false;
        }
    }

    private void RefreshHeader()
    {
        var rpcs3 = App.Services.Config.Rpcs3Path;
        var pes3 = App.Services.Paths.Pes3Root ?? "(configure RPCS3)";
        var mode = Pes3StorageModeResolver.Resolve(App.Services.Config);
        SubtitleText.Text = $"RPCS3: {Path.GetFileName(rpcs3)}  •  PES3: {pes3}";
        FooterText.Text = $"Library: {App.Services.Paths.LibraryRoot}. {Pes3StorageModeResolver.Describe(mode)}";
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await RunScanAsync();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this };
        if (dlg.ShowDialog() == true)
            RefreshHeader();
    }

    private void ReportBug_Click(object sender, RoutedEventArgs e)
    {
        new ReportBugWindow { Owner = this }.ShowDialog();
    }

    private async Task RunScanAsync()
    {
        if (_scanInProgress)
            return;
        _scanInProgress = true;
        try
        {
            if (App.Services.Config.CleanupSessionsOnDiscEject)
                App.Services.SessionRegistry.CleanupEjectedVolumes();

            StatusBanner.Text = "Scanning optical drives…";
            DiscListPanel.Children.Clear();

            var drives = DiscDetector.GetOpticalDrives();
            var cards = 0;

            await Task.Run(() =>
            {
                foreach (var drive in drives)
                {
                    var status = DiscDetector.GetVolumeStatus(drive.Root);
                    Dispatcher.Invoke(() => AddDriveCard(drive, status, ref cards));
                }
            });

            if (cards == 0)
            {
                DiscListPanel.Children.Add(new TextBlock
                {
                    Text = "No PS3 discs detected. Insert a disc and click Scan.",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0),
                });
            }

            StatusBanner.Text = drives.Count > 0
                ? $"Found {drives.Count} optical drive(s). {cards} PS3 volume(s) ready."
                : "No optical drives ready. Insert a disc and wait for Windows to mount it.";
        }
        finally
        {
            _scanInProgress = false;
        }
    }

    private void AddDriveCard(OpticalDrive drive, DiscVolumeStatus status, ref int cards)
    {
        if (status.Kind == DiscVolumeKind.NoPs3Layout &&
            !App.Services.Config.DecryptUnknownOpticalMedia)
            return;

        cards++;
        var border = new Border { Style = (Style)FindResource("CardBorder") };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        var title = status.Game?.Title ?? status.Kind switch
        {
            DiscVolumeKind.EncryptedRetail => "Retail PS3 disc",
            DiscVolumeKind.IncompleteBurn => "Incomplete PS3 layout",
            _ => $"Drive {drive.Letter}:",
        };
        stack.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 16 });

        var detail = status.Message;
        if (status.Kind == DiscVolumeKind.Playable && status.Game is not null)
        {
            var cached = App.Services.Cache.TryGetCached(drive.Id, status.Game.TitleId, null);
            detail = cached is not null
                ? "Library hit — instant play from SSD."
                : Pes3StorageModeResolver.Resolve(App.Services.Config) == Pes3StorageMode.SmartHybrid
                    ? "Disc-assisted: small SSD session, bulk read from disc."
                    : Pes3StorageModeResolver.AllowsDiscDirect(App.Services.Config)
                        ? "Play from disc (no copy) or ephemeral session."
                        : "Will stage into the PES3 library for fast RPCS3 loading.";
        }
        else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn)
        {
            var cached = App.Services.Cache.TryGetCached(drive.Id, null, null)
                ?? App.Services.Cache.TryGetSoleIndexedRetail();
            if (cached is not null)
                detail = "Title in library — Play from library (no re-decrypt).";
            else if (Pes3StorageModeResolver.KeepsPersistentLibrary(App.Services.Config))
                detail = "First decrypt takes 30–90+ min; library mode keeps the next insert instant.";
            else
                detail = "Decrypt per session; removed when RPCS3 exits or disc ejects.";
        }

        stack.Children.Add(new TextBlock
        {
            Text = $"{detail}  ({drive.Root})",
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        if (status.Kind == DiscVolumeKind.Playable && status.Game is not null)
        {
            var cached = App.Services.Cache.TryGetCached(drive.Id, status.Game.TitleId, null);
            var play = new Button
            {
                Content = cached is not null ? "Play from library" : "Play",
                Style = (Style)FindResource("PrimaryButton"),
                Margin = new Thickness(0, 0, 8, 0),
            };
            play.Click += async (_, _) => await PlayGameAsync(drive, status.Game);
            actions.Children.Add(play);
        }
        else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn)
        {
            if (App.Services.Config.EnableRetailDecrypt)
            {
                var cached = App.Services.Cache.TryGetCached(drive.Id, null, null)
                    ?? App.Services.Cache.TryGetSoleIndexedRetail();
                if (cached is not null)
                {
                    var playCache = new Button
                    {
                        Content = "Play from library",
                        Style = (Style)FindResource("PrimaryButton"),
                        Margin = new Thickness(0, 0, 8, 0),
                    };
                    playCache.Click += async (_, _) => await PlayFromCacheAsync(cached);
                    actions.Children.Add(playCache);
                }

                var dec = new Button
                {
                    Content = cached is not null ? "Decrypt again" : "Decrypt & play",
                    Style = (Style)FindResource("SecondaryButton"),
                };
                if (!App.Services.Decryptor.IsAvailable)
                {
                    dec.IsEnabled = false;
                    dec.ToolTip = "Build pes3-disc-dump.exe (re-run Build-App.ps1 with .NET 10 SDK). Not used on Linux.";
                }
                dec.Click += async (_, _) => await DecryptAndPlayAsync(drive);
                actions.Children.Add(dec);
            }
        }

        var dismiss = new Button { Content = "Dismiss", Style = (Style)FindResource("SecondaryButton"), Margin = new Thickness(8, 0, 0, 0) };
        dismiss.Click += (_, _) =>
        {
            _prompted.Add(drive.Id);
            App.Services.Prompted.Save(_prompted);
            border.Visibility = Visibility.Collapsed;
        };
        if (_prompted.Contains(drive.Id))
            border.Visibility = Visibility.Collapsed;
        actions.Children.Add(dismiss);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        border.Child = grid;
        DiscListPanel.Children.Add(border);
    }

    private async Task PlayGameAsync(OpticalDrive drive, DetectedGame game)
    {
        _prompted.Add(drive.Id);
        App.Services.Prompted.Save(_prompted);

        PlaySession session;
        var cached = App.Services.Cache.TryGetCached(drive.Id, game.TitleId, null);
        if (cached is null && !LegalTermsWindow.Prompt(this))
            return;
        if (cached is not null)
        {
            session = App.Services.Cache.SessionFromCached(cached);
            StatusBanner.Text = $"Playing from cache: {game.Title}";
        }
        else
        {
            var stage = new StageWindow(drive, game, game.Title ?? "PS3 game") { Owner = this };
            if (stage.ShowDialog() != true || stage.Session is null)
            {
                if (!string.IsNullOrEmpty(stage.ErrorMessage))
                    MessageBox.Show(this, stage.ErrorMessage, "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            session = stage.Session;
            StatusBanner.Text = session.FromCache
                ? $"Playing from cache: {game.Title}"
                : $"Cached and playing: {game.Title}";
        }

        await LaunchSessionAsync(session, game.Title);
    }

    private async Task PlayFromCacheAsync(CachedGameEntry cached)
    {
        var session = App.Services.Cache.SessionFromCached(cached);
        StatusBanner.Text = $"Playing from cache: {cached.Game.Title}";
        await LaunchSessionAsync(session, cached.Game.Title);
    }

    private async Task LaunchSessionAsync(PlaySession session, string? title)
    {
        var proc = await App.Services.Launcher.LaunchSessionAsync(session);
        if (proc is null)
        {
            MessageBox.Show(this, "Could not start RPCS3. Check Settings.", "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (session.OverlayStats is { } stats)
            StatusBanner.Text = $"Disc-assisted session — {stats.Summary}";

        await Task.CompletedTask;
    }

    private async Task DecryptAndPlayAsync(OpticalDrive drive)
    {
        if (!LegalTermsWindow.Prompt(this))
            return;

        if (!App.Services.Decryptor.IsAvailable)
        {
            MessageBox.Show(this,
                "pes3-disc-dump.exe is missing from the dist folder.\n\nRe-run Build-App.ps1 (install .NET 10 SDK to include retail decrypt).",
                "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _prompted.Add(drive.Id);
        App.Services.Prompted.Save(_prompted);

        var cfg = App.Services.Config;
        var cache = App.Services.Cache;
        var paths = App.Services.Paths;

        var cached = await cache.TryGetRetailCachedAsync(
            drive,
            token => App.Services.Decryptor.ProbeDiscAsync(drive, token)).ConfigureAwait(true);
        if (cached is not null)
        {
            await PlayFromCacheAsync(cached);
            return;
        }

        DiscProbeResult? probe = null;
        try
        {
            probe = await App.Services.Decryptor.ProbeDiscAsync(drive).ConfigureAwait(true);
        }
        catch
        {
            // ignore
        }

        var mode = Pes3StorageModeResolver.Resolve(cfg);
        var outputDir = cache.ResolveRetailOutputDir(probe?.ProductCode);
        Directory.CreateDirectory(outputDir);
        var cleanup = mode == Pes3StorageMode.PersistentLibrary
            ? new List<string>()
            : new List<string> { outputDir };

        var dlg = new DecryptWindow(drive, outputDir) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is not { Success: true } result)
        {
            if (mode != Pes3StorageMode.PersistentLibrary && Directory.Exists(outputDir))
            {
                try { Directory.Delete(outputDir, true); } catch { /* ignore */ }
            }

            var err = dlg.Result?.ErrorMessage ?? "Decryption was not completed.";
            MessageBox.Show(this, err, "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var session = cache.FinalizeRetailDecrypt(result, outputDir, cleanup);
        session = new PlaySession
        {
            EbootPath = session.EbootPath,
            CleanupDirs = session.CleanupDirs,
            FromCache = session.FromCache,
            CacheDir = session.CacheDir,
            Tier = session.Tier,
            VolumeId = drive.Id,
            DiscRoot = drive.Root,
        };
        await LaunchSessionAsync(session, result.Title ?? result.ProductCode);
    }
}
