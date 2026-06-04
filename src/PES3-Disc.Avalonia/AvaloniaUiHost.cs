using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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
            UiDialogs.ShowWarning(owner, $"Could not prepare the game session.\n\n{err}");
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
        if (dlg.Result is { Success: false, ErrorMessage: { } err })
            UiDialogs.ShowWarning(owner, err);
        return dlg.Result;
    }

    public void ShowWarning(string message) => UiDialogs.ShowWarning(GetOwner(), message);

    public void ShowInfo(string message) => UiDialogs.ShowInfo(GetOwner(), message);

    private static Window GetOwner()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
            return d.MainWindow ?? throw new InvalidOperationException("No main window.");
        throw new InvalidOperationException("No desktop lifetime.");
    }
}
