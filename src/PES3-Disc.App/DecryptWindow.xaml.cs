using System.Windows;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class DecryptWindow : Window
{
    private readonly char _driveLetter;
    private readonly string _outputDir;
    private CancellationTokenSource? _cts;
    public DecryptResult? Result { get; private set; }

    public DecryptWindow(char driveLetter, string outputDir)
    {
        InitializeComponent();
        _driveLetter = driveLetter;
        _outputDir = outputDir;
        TitleText.Text = $"Decrypting drive {_driveLetter}:";
        DetailText.Text = "This may take 30–90+ minutes for large games.";
    }

    private bool _started;

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_started) return;
        _started = true;
        _cts = new CancellationTokenSource();
        var progress = new Progress<DecryptProgress>(UpdateProgress);

        try
        {
            Result = await App.Services.Decryptor.DecryptAsync(
                _driveLetter,
                _outputDir,
                progress,
                _cts.Token);
            DialogResult = Result.Success;
            Close();
        }
        catch (Exception ex)
        {
            Result = new DecryptResult { Success = false, ErrorCode = "error", ErrorMessage = ex.Message };
            DialogResult = false;
            Close();
        }
    }

    private void UpdateProgress(DecryptProgress p)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(p.Title))
                TitleText.Text = $"Decrypting: {p.Title}";
            if (p.Phase == "key")
                DetailText.Text = "Looking up decryption key…";
            else if (p.TotalFileSectors > 0)
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
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Result = new DecryptResult { Success = false, ErrorCode = "cancelled", ErrorMessage = "Cancelled." };
        DialogResult = false;
        Close();
    }
}
