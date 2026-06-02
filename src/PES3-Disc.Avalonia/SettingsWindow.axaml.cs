using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace PES3Disc.Avalonia;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var c = App.Services.Config;
        Rpcs3Box.Text = c.Rpcs3Path;
        DeleteCacheCheck.IsChecked = c.DeleteCacheAfterPlay;
        CachePathBox.Text = c.DumpCachePath;
        RetailCheck.IsChecked = c.EnableRetailDecrypt;
        DumpCliBox.Text = c.DumpCliPath;
        IrdDirBox.Text = c.IrdDir;
        BackupsCheck.IsChecked = c.EnableBackups;
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

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Save_Click(object? sender, RoutedEventArgs e)
    {
        var path = Rpcs3Box.Text?.Trim() ?? "";
        if (!File.Exists(path))
        {
            new AvaloniaUiHost().ShowWarning("Select a valid rpcs3 executable.");
            return;
        }

        var c = App.Services.Config;
        c.Rpcs3Path = path;
        c.DeleteCacheAfterPlay = DeleteCacheCheck.IsChecked == true;
        c.DumpCachePath = CachePathBox.Text?.Trim() ?? "";
        c.EnableRetailDecrypt = RetailCheck.IsChecked == true;
        c.DumpCliPath = DumpCliBox.Text?.Trim() ?? "";
        c.IrdDir = IrdDirBox.Text?.Trim() ?? "";
        c.EnableBackups = BackupsCheck.IsChecked == true;
        c.SetupComplete = true;
        App.Services.SaveConfig();
        App.Controller = new PES3Disc.ViewModels.Pes3AppController(App.Services);
        Close(true);
    }
}
