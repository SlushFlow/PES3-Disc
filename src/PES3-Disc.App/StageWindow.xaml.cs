using System.Windows;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class StageWindow : Window
{
    private readonly string _displayTitle;
    private readonly Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>> _work;
    private CancellationTokenSource? _cts;
    public PlaySession? Session { get; private set; }
    public string? ErrorMessage { get; private set; }

    public StageWindow(
        OpticalDrive drive,
        DetectedGame game,
        string title,
        Func<IProgress<StageProgress>, CancellationToken, Task<PlaySession>>? work = null)
    {
        InitializeComponent();
        _displayTitle = title;
        _work = work ?? ((progress, token) =>
            App.Services.Cache.PrepareDiyPlayAsync(drive, game, progress, token));
        TitleText.Text = $"Preparing play: {_displayTitle}";
    }

    private bool _started;

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_started)
            return;
        _started = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<StageProgress>(UpdateProgress);

        try
        {
            Session = await _work(progress, _cts.Token);
            if (Session.OverlayStats is { } stats)
            {
                TitleText.Text = $"Ready: {_displayTitle}";
                DetailText.Text = stats.Summary;
            }
            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Cancelled.";
            DialogResult = false;
            Close();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            DialogResult = false;
            Close();
        }
    }

    private void UpdateProgress(StageProgress p)
    {
        Dispatcher.Invoke(() =>
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
            if (!string.IsNullOrEmpty(p.Status) && p.TotalFiles > 0)
                DetailText.Text = p.Status;
        });
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        ErrorMessage = "Cancelled.";
        DialogResult = false;
        Close();
    }
}
