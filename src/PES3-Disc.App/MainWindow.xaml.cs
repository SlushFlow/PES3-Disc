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
        SubtitleText.Text = $"RPCS3: {Path.GetFileName(rpcs3)}  •  PES3: {pes3}";
        FooterText.Text = "Official discs need a compatible Blu-ray drive. Decrypt once, then play in RPCS3.";
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
                ? $"Found {drives.Count} optical drive(s). Showing {cards} PS3-related volume(s)."
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
        stack.Children.Add(new TextBlock
        {
            Text = $"{status.Message}  ({drive.Root})",
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        if (status.Kind == DiscVolumeKind.Playable && status.Game is not null)
        {
            var play = new Button { Content = "Play", Style = (Style)FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 0) };
            play.Click += async (_, _) => await PlayGameAsync(drive, status.Game);
            actions.Children.Add(play);
        }
        else if (status.Kind is DiscVolumeKind.EncryptedRetail or DiscVolumeKind.IncompleteBurn)
        {
            if (App.Services.Config.EnableRetailDecrypt)
            {
                var dec = new Button { Content = "Decrypt & play", Style = (Style)FindResource("PrimaryButton") };
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

        var cleanup = new List<string>();
        var proc = App.Services.Launcher.LaunchGame(game.EbootPath, cleanup);
        if (proc is null)
        {
            MessageBox.Show(this, "Could not start RPCS3. Check Settings.", "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StatusBanner.Text = $"Playing: {game.Title}";
        await Task.CompletedTask;
    }

    private async Task DecryptAndPlayAsync(OpticalDrive drive)
    {
        if (_prompted.Contains(drive.Id))
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
        var paths = App.Services.Paths;
        string outputDir;
        List<string> cleanup;

        if (cfg.DeleteCacheAfterPlay)
        {
            outputDir = paths.NewSessionDir();
            cleanup = new List<string> { outputDir };
        }
        else
        {
            outputDir = Path.Combine(paths.CacheRoot, $"dump-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(outputDir);
            cleanup = new List<string>();
        }

        var dlg = new DecryptWindow(drive.Letter, outputDir) { Owner = this };
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

        string eboot = result.Eboot!;
        if (!cfg.DeleteCacheAfterPlay && !string.IsNullOrEmpty(result.ProductCode) && result.GameRoot is not null)
        {
            var final = Path.Combine(paths.CacheRoot, result.ProductCode);
            if (Directory.Exists(final))
            {
                try { Directory.Delete(final, true); } catch { /* ignore */ }
            }
            try
            {
                Directory.Move(result.GameRoot, final);
                eboot = Path.Combine(final, "PS3_GAME", "USRDIR", "EBOOT.BIN");
                cleanup.Add(final);
            }
            catch
            {
                cleanup.Add(result.GameRoot);
            }
        }
        else if (result.GameRoot is not null)
        {
            cleanup.Add(result.GameRoot);
        }

        App.Services.Launcher.LaunchGame(eboot, cleanup);
        StatusBanner.Text = $"Playing: {result.Title ?? result.ProductCode}";
        await Task.CompletedTask;
    }
}
