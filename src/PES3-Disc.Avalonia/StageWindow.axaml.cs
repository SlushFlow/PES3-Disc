using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PES3Disc.Core;

namespace PES3Disc.Avalonia;

public partial class StageWindow : Window
{
    private readonly Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> _work;
    private CancellationTokenSource? _cts;
    public PlaySession? Session { get; private set; }

    public StageWindow(
        OpticalDrive drive,
        DetectedGame game,
        Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> work)
    {
        InitializeComponent();
        _work = work;
        TitleText.Text = $"Preparing cache: {game.Title ?? drive.DisplayName}";
        DetailText.Text = "RPCS3 loads faster from SSD than from the optical drive.";
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _cts = new CancellationTokenSource();
        var progress = new Progress<StageProgress>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (p.TotalFiles > 0)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = p.Percent;
                    PercentText.Text = $"{p.FilesCopied} / {p.TotalFiles} files — {p.Percent}%";
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                    PercentText.Text = p.Status ?? "Copying…";
                }
            });
        });

        try
        {
            Session = await _work(progress, _cts.Token);
            Close(true);
        }
        catch (OperationCanceledException)
        {
            Close(false);
        }
        catch
        {
            Close(false);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close(false);
    }
}
