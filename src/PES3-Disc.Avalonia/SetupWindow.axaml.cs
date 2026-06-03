using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PES3Disc.Core;

namespace PES3Disc.Avalonia;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        var c = App.Services.Config;
        DeleteCacheCheck.IsChecked = c.DeleteCacheAfterPlay;
        RetailCheck.IsChecked = c.EnableRetailDecrypt;
        var detected = App.Services.Launcher.FindRpcs3();
        if (detected is not null)
            Rpcs3Box.Text = detected;
    }

    private async void Browse_Click(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select rpcs3",
            AllowMultiple = false,
        });
        if (files.Count > 0)
            Rpcs3Box.Text = files[0].Path.LocalPath;
    }

    private void Quit_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void OpenLegal_Click(object? sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("LEGAL.md");

    private void Continue_Click(object? sender, RoutedEventArgs e)
    {
        var path = Rpcs3Box.Text?.Trim() ?? "";
        if (!File.Exists(path))
        {
            new AvaloniaUiHost().ShowWarning("Select a valid rpcs3 executable.");
            return;
        }

        if (LegalOwnCheck.IsChecked != true || LegalNoShareCheck.IsChecked != true || LegalComplyCheck.IsChecked != true)
        {
            new AvaloniaUiHost().ShowWarning("Confirm all legal statements to continue.");
            return;
        }

        var c = App.Services.Config;
        c.Rpcs3Path = path;
        c.DeleteCacheAfterPlay = DeleteCacheCheck.IsChecked == true;
        c.EnableRetailDecrypt = RetailCheck.IsChecked == true;
        c.SetupComplete = true;
        LegalTerms.RecordAcceptance(c);
        App.Services.SaveConfig();
        App.Controller = new PES3Disc.ViewModels.Pes3AppController(App.Services);
        Close(true);
    }
}
