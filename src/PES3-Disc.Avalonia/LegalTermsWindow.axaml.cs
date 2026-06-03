using Avalonia.Controls;
using Avalonia.Interactivity;
using PES3Disc.Core;

namespace PES3Disc.Avalonia;

public partial class LegalTermsWindow : Window
{
    public bool Accepted { get; private set; }

    public LegalTermsWindow()
    {
        InitializeComponent();
        OwnDiscCheck.IsCheckedChanged += (_, _) => UpdateAccept();
        NoRedistributeCheck.IsCheckedChanged += (_, _) => UpdateAccept();
        ComplyLawCheck.IsCheckedChanged += (_, _) => UpdateAccept();
    }

    private void UpdateAccept() =>
        AcceptButton.IsEnabled = OwnDiscCheck.IsChecked == true
            && NoRedistributeCheck.IsChecked == true
            && ComplyLawCheck.IsChecked == true;

    private void OpenLegal_Click(object? sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("LEGAL.md");

    private void OpenGuide_Click(object? sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("USER-LEGAL-GUIDE.md");

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Accepted = false;
        Close(false);
    }

    private void Accept_Click(object? sender, RoutedEventArgs e)
    {
        if (AcceptButton.IsEnabled != true)
            return;
        Accepted = true;
        Close(true);
    }
}
