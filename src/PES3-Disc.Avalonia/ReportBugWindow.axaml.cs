using Avalonia.Controls;
using Avalonia.Interactivity;
using PES3Disc.BugReports;

namespace PES3Disc.Avalonia;

public partial class ReportBugWindow : Window
{
    public ReportBugWindow()
    {
        InitializeComponent();
        UpdateCounts();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e) => UpdateCounts();

    private void UpdateCounts()
    {
        TitleCount.Text = $"{TitleBox.Text?.Length ?? 0}/{BugReportLimits.MaxTitleLength}";
        BodyCount.Text = $"{BodyBox.Text?.Length ?? 0}/{BugReportLimits.MaxBodyLength}";
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private async void Submit_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            BugReportLimits.Validate(TitleBox.Text ?? "", BodyBox.Text ?? "");
        }
        catch (ArgumentException ex)
        {
            StatusText.Text = ex.Message;
            return;
        }

        SubmitButton.IsEnabled = false;
        StatusText.Text = "Sending…";
        try
        {
            await App.Controller.SubmitBugReportAsync(TitleBox.Text!.Trim(), BodyBox.Text!.Trim(), "linux");
            UiDialogs.ShowInfo(this, "Thank you — your report was sent. You will be notified when a developer responds.");
            Close(true);
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            SubmitButton.IsEnabled = true;
        }
    }
}
