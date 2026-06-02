using Avalonia.Controls;
using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.Avalonia;

public sealed class AvaloniaUiHost : IPes3UiHost
{
    public async Task<PlaySession?> ShowStageDialogAsync(
        OpticalDrive drive,
        DetectedGame game,
        Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> work)
    {
        var dlg = new StageWindow(drive, game, work);
        var owner = GetOwner();
        await dlg.ShowDialog(owner);
        return dlg.Session;
    }

    public async Task<DecryptResult?> ShowDecryptDialogAsync(
        OpticalDrive drive,
        string outputDir,
        Func<IProgress<DecryptProgress>, CancellationToken, Task<DecryptResult>> work)
    {
        var dlg = new DecryptWindow(drive, outputDir, work);
        var owner = GetOwner();
        await dlg.ShowDialog(owner);
        return dlg.Result;
    }

    public void ShowWarning(string message)
    {
        var owner = GetOwner();
        var dlg = new Window
        {
            Title = "PES3-Disc",
            Width = 400,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right },
                },
            },
        };
        if (dlg.Content is StackPanel sp && sp.Children[^1] is Button ok)
            ok.Click += (_, _) => dlg.Close();
        dlg.ShowDialog(owner);
    }

    public void ShowInfo(string message) => ShowWarning(message);

    private static Window GetOwner()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
            return d.MainWindow ?? throw new InvalidOperationException("No main window.");
        throw new InvalidOperationException("No desktop lifetime.");
    }
}
