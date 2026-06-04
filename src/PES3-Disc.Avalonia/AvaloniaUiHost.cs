using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.Avalonia;

public sealed class AvaloniaUiHost : IPes3UiHost
{
    public async Task<bool> ConfirmLegalTermsAsync()
    {
        if (LegalTerms.IsAccepted(App.Services.Config))
            return true;

        var dlg = new LegalTermsWindow();
        var owner = GetOwner();
        var ok = await dlg.ShowDialog<bool>(owner);
        if (ok != true || !dlg.Accepted)
            return false;

        LegalTerms.RecordAcceptance(App.Services.Config);
        App.Services.SaveConfig();
        return true;
    }

    public async Task<PlaySession?> ShowStageDialogAsync(
        OpticalDrive drive,
        DetectedGame game,
        Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> work)
    {
        var dlg = new StageWindow(drive, game, work);
        var owner = GetOwner();
        await dlg.ShowDialog(owner);
        if (dlg.Session is null && dlg.ErrorMessage is { } err)
            ShowWarning($"Could not prepare the game session.\n\n{err}");
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
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right },
                },
            },
        };
        if (dlg.Content is StackPanel sp && sp.Children[^1] is Button ok)
            ok.Click += (_, _) => dlg.Close();
        dlg.ShowDialog(owner);
    }

    public void ShowInfo(string message)
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
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right },
                },
            },
        };
        if (dlg.Content is StackPanel sp && sp.Children[^1] is Button ok)
            ok.Click += (_, _) => dlg.Close();
        dlg.ShowDialog(owner);
    }

    private static Window GetOwner()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            return d.MainWindow ?? throw new InvalidOperationException("No main window.");
        throw new InvalidOperationException("No desktop lifetime.");
    }
}
