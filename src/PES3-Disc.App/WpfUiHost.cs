using System.Windows;
using PES3Disc.Core;
using PES3Disc.ViewModels;

namespace PES3Disc.App;

public sealed class WpfUiHost : IPes3UiHost
{
    private readonly Window _owner;

    public WpfUiHost(Window owner) => _owner = owner;

    public Task<bool> ConfirmLegalTermsAsync()
    {
        var ok = LegalTermsWindow.Prompt(_owner);
        return Task.FromResult(ok);
    }

    public Task<PlaySession?> ShowStageDialogAsync(
        OpticalDrive drive,
        DetectedGame game,
        Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> work)
    {
        var dlg = new StageWindow(drive, game, game.Title ?? "PS3 game", work) { Owner = _owner };
        if (dlg.ShowDialog() != true || dlg.Session is null)
        {
            if (!string.IsNullOrEmpty(dlg.ErrorMessage))
                ShowWarning(dlg.ErrorMessage);
            return Task.FromResult<PlaySession?>(null);
        }

        return Task.FromResult<PlaySession?>(dlg.Session);
    }

    public async Task<DecryptResult?> ShowDecryptDialogAsync(
        OpticalDrive drive,
        string outputDir,
        Func<IProgress<DecryptProgress>, CancellationToken, Task<DecryptResult>> work)
    {
        var dlg = new DecryptWindow(drive, outputDir, work) { Owner = _owner };
        if (dlg.ShowDialog() != true)
            return null;
        return dlg.Result;
    }

    public void ShowWarning(string message) =>
        MessageBox.Show(_owner, message, "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowInfo(string message) =>
        MessageBox.Show(_owner, message, "PES3-Disc", MessageBoxButton.OK, MessageBoxImage.Information);
}
