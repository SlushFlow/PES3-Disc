using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PES3Disc.Core;

namespace PES3Disc.Avalonia;

public partial class SettingsWindow : Window
{
    private static readonly (Pes3StorageMode Mode, string Label)[] StorageItems =
    {
        (Pes3StorageMode.SmartHybrid, "Smart hybrid (recommended)"),
        (Pes3StorageMode.PersistentLibrary, "Persistent library"),
        (Pes3StorageMode.EphemeralSession, "Ephemeral session"),
        (Pes3StorageMode.DiscDirect, "Play DIY from disc (no copy)"),
    };

    public SettingsWindow()
    {
        InitializeComponent();
        var c = App.Services.Config;
        Rpcs3Box.Text = c.Rpcs3Path;
        StorageModeCombo.ItemsSource = StorageItems.Select(t => t.Label).ToList();
        SelectStorageMode(Pes3StorageModeResolver.Resolve(c));
        StorageModeCombo.SelectionChanged += (_, _) => UpdateStorageHint();
        CachePathBox.Text = c.DumpCachePath;
        RetailCheck.IsChecked = c.EnableRetailDecrypt;
        DumpCliBox.Text = c.DumpCliPath;
        IrdDirBox.Text = c.IrdDir;
        BackupsCheck.IsChecked = c.EnableBackups;
        EjectCleanupCheck.IsChecked = c.CleanupSessionsOnDiscEject;
        OverlayMbBox.Text = c.OverlayMaxLocalMegabytes.ToString();
        BugReportApiBox.Text = c.BugReportApiUrl;
    }

    private void SelectStorageMode(Pes3StorageMode mode)
    {
        var idx = Array.FindIndex(StorageItems, t => t.Mode == mode);
        StorageModeCombo.SelectedIndex = idx >= 0 ? idx : 0;
        UpdateStorageHint();
    }

    private void UpdateStorageHint()
    {
        var idx = StorageModeCombo.SelectedIndex;
        if (idx < 0 || idx >= StorageItems.Length)
            return;
        StorageModeHint.Text = Pes3StorageModeResolver.Describe(StorageItems[idx].Mode);
    }

    private Pes3StorageMode SelectedStorageMode()
    {
        var idx = StorageModeCombo.SelectedIndex;
        return idx >= 0 && idx < StorageItems.Length
            ? StorageItems[idx].Mode
            : Pes3StorageMode.SmartHybrid;
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
            UiDialogs.ShowWarning(this, "Select a valid rpcs3 executable.");
            return;
        }

        var c = App.Services.Config;
        c.Rpcs3Path = path;
        Pes3StorageModeResolver.Apply(c, SelectedStorageMode());
        c.DumpCachePath = CachePathBox.Text?.Trim() ?? "";
        c.EnableRetailDecrypt = RetailCheck.IsChecked == true;
        c.DumpCliPath = DumpCliBox.Text?.Trim() ?? "";
        c.IrdDir = IrdDirBox.Text?.Trim() ?? "";
        c.EnableBackups = BackupsCheck.IsChecked == true;
        c.CleanupSessionsOnDiscEject = EjectCleanupCheck.IsChecked == true;
        if (int.TryParse(OverlayMbBox.Text?.Trim(), out var overlayMb))
            c.OverlayMaxLocalMegabytes = Math.Clamp(overlayMb, 64, 32_768);
        c.BugReportApiUrl = BugReportApiBox.Text?.Trim() ?? "";
        c.SetupComplete = true;
        App.Services.SaveConfig();
        App.Controller = new PES3Disc.ViewModels.Pes3AppController(App.Services);
        Close(true);
    }
}
