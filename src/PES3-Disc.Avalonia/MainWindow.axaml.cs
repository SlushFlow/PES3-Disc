using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.Avalonia;

public partial class MainWindow : Window
{
    private readonly AvaloniaUiHost _ui = new();
    private readonly DispatcherTimer _scanTimer;
    private bool _scanInProgress;

    public MainWindow()
    {
        InitializeComponent();
        RefreshHeader();
        _scanTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Max(2, App.Services.Config.ScanDelaySeconds)),
        };
        _scanTimer.Tick += async (_, _) => await RunScanAsync();
        _scanTimer.Start();
        Opened += async (_, _) => await RunScanAsync();
    }

    private void RefreshHeader()
    {
        SubtitleText.Text = App.Controller.Subtitle;
        FooterText.Text = App.Controller.Footer;
    }

    private async void Scan_Click(object? sender, RoutedEventArgs e) => await RunScanAsync();

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow();
        if (await dlg.ShowDialog<bool>(this))
            RefreshHeader();
    }

    private async void ReportBug_Click(object? sender, RoutedEventArgs e)
    {
        var dlg = new ReportBugWindow();
        await dlg.ShowDialog<bool>(this);
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
                    Classes = { "muted" },
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 40, 0, 0),
                });
            }

            StatusBanner.Text = cards.Count > 0
                ? $"Found {cards.Count} PS3 volume(s) ready."
                : "No optical drives ready. Mount a disc under /media or /run/media.";
        }
        finally
        {
            _scanInProgress = false;
        }
    }

    private void AddDriveCard(DiscCardModel card)
    {
        var border = new Border { Classes = { "card" } };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = card.Title,
            FontWeight = FontWeight.SemiBold,
            FontSize = 16,
            Foreground = Brushes.White,
        });
        stack.Children.Add(new TextBlock
        {
            Text = card.Detail,
            Classes = { "muted" },
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (card.CanPlay && card.Status.Game is { } game)
        {
            var play = new Button
            {
                Content = card.PlayButtonText,
                Classes = { "primary" },
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
            if (card.CanDecryptAgain)
            {
                var playCache = new Button { Content = "Play from cache", Classes = { "primary" } };
                playCache.Click += async (_, _) =>
                {
                    var cached = App.Services.Cache.TryGetCached(card.Drive.Id, null, null);
                    if (cached is not null)
                    {
                        StatusBanner.Text = $"Playing from cache: {cached.Game.Title}";
                        await App.Controller.PlayFromCacheAsync(cached, _ui);
                    }
                };
                actions.Children.Add(playCache);
            }

            var dec = new Button
            {
                Content = card.CanDecryptAgain ? "Decrypt again" : "Decrypt & play",
                Classes = { "secondary" },
                IsEnabled = card.DecryptAvailable,
            };
            dec.Click += async (_, _) =>
            {
                StatusBanner.Text = "Decrypting…";
                var msg = await App.Controller.DecryptAndPlayAsync(card.Drive, _ui);
                if (msg is not null)
                    StatusBanner.Text = "Playing from decrypted cache.";
            };
            actions.Children.Add(dec);
        }

        var dismiss = new Button { Content = "Dismiss", Classes = { "secondary" } };
        dismiss.Click += (_, _) =>
        {
            App.Controller.Dismiss(card.Drive);
            border.IsVisible = false;
        };
        actions.Children.Add(dismiss);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);
        border.Child = grid;
        DiscListPanel.Children.Add(border);
    }
}
