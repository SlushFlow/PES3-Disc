using System.Windows;
using Microsoft.Win32;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        var c = App.Services.Config;
        Rpcs3Box.Text = c.Rpcs3Path;
        NoGuiCheck.IsChecked = c.UseNoGui;
        DeleteCacheCheck.IsChecked = c.DeleteCacheAfterPlay;
        CachePathBox.Text = c.DumpCachePath;
        RetailCheck.IsChecked = c.EnableRetailDecrypt;
        UnknownCheck.IsChecked = c.DecryptUnknownOpticalMedia;
        DumpCliBox.Text = c.DumpCliPath;
        IrdDirBox.Text = c.IrdDir;
        BackupsCheck.IsChecked = c.EnableBackups;
        BackupOnLaunchCheck.IsChecked = c.BackupOnLaunch;
        StartupCheck.IsChecked = c.RunAtStartup;
        DelayBox.Text = c.ScanDelaySeconds.ToString();
        BugReportApiBox.Text = c.BugReportApiUrl;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "RPCS3|rpcs3.exe", FileName = "rpcs3.exe" };
        if (dlg.ShowDialog() == true)
            Rpcs3Box.Text = dlg.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var path = Rpcs3Box.Text.Trim();
        if (!File.Exists(path))
        {
            MessageBox.Show(this, "Select a valid rpcs3.exe.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(DelayBox.Text.Trim(), out var delay) || delay < 0)
            delay = 3;

        var c = App.Services.Config;
        c.Rpcs3Path = path;
        c.UseNoGui = NoGuiCheck.IsChecked == true;
        c.DeleteCacheAfterPlay = DeleteCacheCheck.IsChecked == true;
        c.DumpCachePath = CachePathBox.Text.Trim();
        c.EnableRetailDecrypt = RetailCheck.IsChecked == true;
        c.DecryptUnknownOpticalMedia = UnknownCheck.IsChecked == true;
        c.DumpCliPath = DumpCliBox.Text.Trim();
        c.IrdDir = IrdDirBox.Text.Trim();
        c.EnableBackups = BackupsCheck.IsChecked == true;
        c.BackupOnLaunch = BackupOnLaunchCheck.IsChecked == true;
        c.RunAtStartup = StartupCheck.IsChecked == true;
        c.ScanDelaySeconds = delay;
        c.BugReportApiUrl = BugReportApiBox.Text.Trim();
        c.SetupComplete = true;
        App.Services.SaveConfig();

        var exe = Environment.ProcessPath ?? "";
        if (c.RunAtStartup && !string.IsNullOrEmpty(exe))
            StartupShortcut.Install(exe);
        else
            StartupShortcut.Remove();

        DialogResult = true;
        Close();
    }
}
