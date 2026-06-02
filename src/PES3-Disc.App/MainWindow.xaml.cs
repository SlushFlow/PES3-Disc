using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _scanTimer;
    private readonly HashSet<string> _prompted;
    private bool _scanInProgress;

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
        Loaded += async (_, _) => await RunScanAsync();
    }

    private void RefreshHeader()
    {
        var rpcs3 = App.Services.Config.Rpcs3Path;
        var pes3 = App.Services.Paths.Pes3Root ?? "(configure RPCS3)";
        var cacheNote = App.Services.Config.DeleteCacheAfterPlay
            ? "Session cache (cleared after play)"
            : $"Persistent cache: {App.Services.Paths.CacheRoot}";
        SubtitleText.Text = $"RPCS3: {Path.GetFileName(rpcs3)}  •  PES3: {pes3}";
        FooterText.Text = $"DIY and retail discs use the same PES3 cache for fast RPCS3 loading. {cacheNote}.";
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await RunScanAsync();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this };
        if (dlg.ShowDialog() == true)
            RefreshHeader();
    }

    private async Task RunScanAsync()
    {
        if (_scanInProgress)
            return;
        _scanInProgress = true;
        try
        {
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
                ? "Cached copy on disk — instant play from SSD."
                : "Will copy to PES3 cache for the same fast loading as decrypted retail games.";
        }
        else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn)
        {
            var cached = App.Services.Cache.TryGetCached(drive.Id, null, null);
            if (cached is not null)
                detail = "Decrypted game already in cache — play without re-decrypting.";
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
                Content = cached is not null ? "Play from cache" : "Play",
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
                var cached = App.Services.Cache.TryGetCached(drive.Id, null, null);
                if (cached is not null)
                {
                    var playCache = new Button
                    {
                        Content = "Play from cache",
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
                    dec.ToolTip = "Build pes3-disc-dump.exe (re-run Build-App.ps1 with .NET 10 SDK).";
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
        var proc = await App.Services.Launcher.LaunchGameAsync(
            session.EbootPath,
            session.CleanupDirs.ToList());
        if (proc is null)
        {
            MessageBox.Show(this, "Could not start RPCS3. Check Settings.", "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await Task.CompletedTask;
    }

    private async Task DecryptAndPlayAsync(OpticalDrive drive)
    {
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

        var cached = cache.TryGetCached(drive.Id, null, null);
        if (cached is not null)
        {
            await PlayFromCacheAsync(cached);
            return;
        }

        string outputDir;
        List<string> cleanup;

        if (cfg.DeleteCacheAfterPlay)
        {
            outputDir = paths.NewSessionDir();
            cleanup = new List<string> { outputDir };
        }
        else
        {
            outputDir = cache.ResolveRetailOutputDir(null);
            Directory.CreateDirectory(outputDir);
            cleanup = new List<string>();
        }

        var dlg = new DecryptWindow(drive, outputDir) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.Result is not { Success: true } result)
        {
            if (cfg.DeleteCacheAfterPlay && Directory.Exists(outputDir))
            {
                try { Directory.Delete(outputDir, true); } catch { /* ignore */ }
            }

            var err = dlg.Result?.ErrorMessage ?? "Decryption was not completed.";
            MessageBox.Show(this, err, "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var session = cache.FinalizeRetailDecrypt(result, outputDir, cleanup);
        await LaunchSessionAsync(session, result.Title ?? result.ProductCode);
    }
}
