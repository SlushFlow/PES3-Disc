using System.Windows;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class LegalTermsWindow : Window
{
    public bool Accepted { get; private set; }

    public LegalTermsWindow()
    {
        InitializeComponent();
        OwnDiscCheck.Checked += (_, _) => UpdateAccept();
        OwnDiscCheck.Unchecked += (_, _) => UpdateAccept();
        NoRedistributeCheck.Checked += (_, _) => UpdateAccept();
        NoRedistributeCheck.Unchecked += (_, _) => UpdateAccept();
        ComplyLawCheck.Checked += (_, _) => UpdateAccept();
        ComplyLawCheck.Unchecked += (_, _) => UpdateAccept();
    }

    private void UpdateAccept() =>
        AcceptButton.IsEnabled = OwnDiscCheck.IsChecked == true
            && NoRedistributeCheck.IsChecked == true
            && ComplyLawCheck.IsChecked == true;

    private void OpenLegal_Click(object sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("LEGAL.md");

    private void OpenGuide_Click(object sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("USER-LEGAL-GUIDE.md");

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Accepted = false;
        DialogResult = false;
        Close();
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (AcceptButton.IsEnabled != true)
        {
            HintText.Visibility = Visibility.Visible;
            HintText.Text = "Please confirm all three statements.";
            return;
        }

        Accepted = true;
        DialogResult = true;
        Close();
    }

    public static bool Prompt(Window? owner)
    {
        if (LegalTerms.IsAccepted(App.Services.Config))
            return true;

        var dlg = new LegalTermsWindow { Owner = owner };
        if (dlg.ShowDialog() != true || !dlg.Accepted)
            return false;

        LegalTerms.RecordAcceptance(App.Services.Config);
        App.Services.SaveConfig();
        Pes3Log.Write($"Legal terms accepted ({LegalTerms.CurrentVersion}).");
        return true;
    }
}
