using Avalonia.Controls;
using Avalonia.Interactivity;
using PES3Disc.Core;

namespace PES3Disc.Avalonia;

public partial class DecryptWindow : Window
{
    private readonly Func<IProgress<DecryptProgress>, CancellationToken, Task<DecryptResult>> _work;
    private CancellationTokenSource? _cts;
    private bool _started;
    public DecryptResult? Result { get; private set; }

    public DecryptWindow(
        OpticalDrive drive,
        string outputDir,
        Func<IProgress<DecryptProgress>, CancellationToken, Task<DecryptResult>> work)
    {
        InitializeComponent();
        _work = work;
        TitleText.Text = $"Decrypting {drive.DisplayName}:";
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (_started) return;
        _started = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<DecryptProgress>(p =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrEmpty(p.Title))
                    TitleText.Text = $"Decrypting: {p.Title}";
                if (p.TotalFileSectors > 0)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = p.Percent;
                    PercentText.Text = $"File {p.CurrentFile} of {p.TotalFiles} — {p.Percent}%";
                }
                else
                {
                    ProgressBar.IsIndeterminate = true;
                    PercentText.Text = "Analyzing disc…";
                }
            });
        });

        try
        {
            Result = await _work(progress, _cts.Token);
            Close(Result?.Success == true);
        }
        catch (OperationCanceledException)
        {
            Result = new DecryptResult { Success = false, ErrorCode = "cancelled", ErrorMessage = "Cancelled." };
            Close(false);
        }
        catch (Exception ex)
        {
            Result = new DecryptResult { Success = false, ErrorCode = "error", ErrorMessage = ex.Message };
            Close(false);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close(false);
    }
}
