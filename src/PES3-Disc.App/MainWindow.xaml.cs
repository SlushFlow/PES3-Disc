using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using PES3Disc.BugReports;
using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.App;

public partial class MainWindow : Window
{
    private readonly WpfUiHost _ui;
    private readonly DispatcherTimer _scanTimer;
    private readonly DispatcherTimer _bugReportTimer;
    private readonly DevStatusTracker _devStatusTracker;
    private readonly BugReportResolutionPoller _bugReportPoller = new();
    private bool _scanInProgress;
    private bool _bugReportPollInProgress;

    public MainWindow()
    {
        InitializeComponent();
        _ui = new WpfUiHost(this);
        RefreshHeader();
        if (App.Services.ConfigLoadWarning is { } warn)
            _ui.ShowWarning(warn);
        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(2, App.Services.Config.ScanDelaySeconds)),
        };
        _scanTimer.Tick += async (_, _) => await RunScanAsync();
        _scanTimer.Start();
        _bugReportTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
        _bugReportTimer.Tick += async (_, _) => await PollBugReportResolutionsAsync();
        _bugReportTimer.Start();
        _devStatusTracker = new DevStatusTracker(App.Controller.BugReportApiUrl);
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
            var notifications = await _bugReportPoller.PollAsync(App.Controller.BugReportApiUrl);
            foreach (var note in notifications)
            {
                _ui.ShowInfo(note.FormatForUser());
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
        SubtitleText.Text = App.Controller.Subtitle;
        FooterText.Text = App.Controller.Footer;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        App.Controller.InvalidateScanCache();
        await RunScanAsync();
    }

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
            App.Controller.CleanupEjectedVolumes();

            StatusBanner.Text = "Scanning optical drives…";
            DiscListPanel.Children.Clear();

            var cards = await Task.Run(() => App.Controller.ScanVolumes());

            foreach (var card in cards)
            {
                if (card.IsDismissed)
                    continue;
                AddDriveCard(card);
            }

            if (DiscListPanel.Children.Count == 0)
            {
                DiscListPanel.Children.Add(new TextBlock
                {
                    Text = "No PS3 discs detected. Insert a disc and click Scan.",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0),
                });
            }

            StatusBanner.Text = cards.Count > 0
                ? $"Found {cards.Count} PS3 volume(s) ready."
                : "No optical drives ready. Insert a disc and wait for Windows to mount it.";
        }
        finally
        {
            _scanInProgress = false;
        }
    }

    private void AddDriveCard(DiscCardModel card)
    {
        var border = new Border { Style = (Style)FindResource("CardBorder") };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = card.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 16,
        });
        stack.Children.Add(new TextBlock
        {
            Text = card.Detail,
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        if (card.CanPlay && card.Status.Game is { } game)
        {
            var play = new Button
            {
                Content = card.PlayButtonText,
                Style = (Style)FindResource("PrimaryButton"),
                Margin = new Thickness(0, 0, 8, 0),
            };
            play.Click += async (_, _) =>
            {
                StatusBanner.Text = $"Playing: {game.Title}";
                await App.Controller.PlayGameAsync(card.Drive, game, _ui);
            };
            actions.Children.Add(play);
        }
        else if (card.CanDecrypt)
        {
            if (card.CanPlayFromCache && card.LibraryEntry is not null)
            {
                var playCache = new Button
                {
                    Content = "Play from library",
                    Style = (Style)FindResource("PrimaryButton"),
                    Margin = new Thickness(0, 0, 8, 0),
                };
                playCache.Click += async (_, _) =>
                {
                    StatusBanner.Text = $"Playing from library: {card.LibraryEntry.Game.Title}";
                    await App.Controller.PlayFromCacheAsync(card.LibraryEntry, _ui);
                };
                actions.Children.Add(playCache);
            }

            var dec = new Button
            {
                Content = card.CanDecryptAgain ? "Decrypt again" : "Decrypt & play",
                Style = (Style)FindResource("SecondaryButton"),
                IsEnabled = card.DecryptAvailable,
            };
            if (!card.DecryptAvailable)
                dec.ToolTip = "Build pes3-disc-dump.exe (re-run Build-App.ps1 with .NET 10 SDK).";
            dec.Click += async (_, _) =>
            {
                StatusBanner.Text = "Decrypting…";
                var msg = await App.Controller.DecryptAndPlayAsync(card.Drive, _ui);
                if (msg is not null)
                    StatusBanner.Text = "Playing from library.";
            };
            actions.Children.Add(dec);
        }

        var dismiss = new Button
        {
            Content = "Dismiss",
            Style = (Style)FindResource("SecondaryButton"),
            Margin = new Thickness(8, 0, 0, 0),
        };
        dismiss.Click += (_, _) =>
        {
            App.Controller.Dismiss(card.Drive);
            border.Visibility = Visibility.Collapsed;
        };
        actions.Children.Add(dismiss);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        border.Child = grid;
        DiscListPanel.Children.Add(border);
    }
}
