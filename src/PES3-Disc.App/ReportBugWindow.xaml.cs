using System.Reflection;
using System.Windows;
using PES3Disc.BugReports;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class ReportBugWindow : Window
{
    public ReportBugWindow()
    {
        InitializeComponent();
        UpdateCounts();
    }

    private void TitleBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateCounts();
    private void BodyBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => UpdateCounts();

    private void UpdateCounts()
    {
        TitleCount.Text = $"{TitleBox.Text.Length}/{BugReportLimits.MaxTitleLength}";
        BodyCount.Text = $"{BodyBox.Text.Length}/{BugReportLimits.MaxBodyLength}";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BugReportLimits.Validate(TitleBox.Text, BodyBox.Text);
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
            var apiUrl = string.IsNullOrWhiteSpace(App.Services.Config.BugReportApiUrl)
                ? BugReportEndpoints.DefaultApiBaseUrl
                : App.Services.Config.BugReportApiUrl.Trim();
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
            using var client = new BugReportClient(apiUrl);
            var result = await client.SubmitAsync(new BugReportSubmission
            {
                Title = TitleBox.Text.Trim(),
                Body = BodyBox.Text.Trim(),
                Platform = "windows",
                AppVersion = version,
                OsDescription = Environment.OSVersion.ToString(),
            });
            BugReportPendingTracker.TrackSubmission(result.Id, TitleBox.Text.Trim());
            MessageBox.Show(this, "Thank you — your report was sent. You will be notified here when a developer responds.", "Report a bug", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            SubmitButton.IsEnabled = true;
        }
    }
}
