using System.Windows;
using Microsoft.Win32;
using PES3Disc.Core;

namespace PES3Disc.App;

public partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        var cfg = App.Services.Config;
        Rpcs3PathBox.Text = cfg.Rpcs3Path;
        DeleteCacheCheck.IsChecked = cfg.DeleteCacheAfterPlay;
        BackupsCheck.IsChecked = cfg.EnableBackups;
        StartupCheck.IsChecked = cfg.RunAtStartup;

        var found = App.Services.Launcher.FindRpcs3();
        if (string.IsNullOrEmpty(cfg.Rpcs3Path) && found is not null)
            Rpcs3PathBox.Text = found;
    }

    private void BrowseRpcs3_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "RPCS3|rpcs3.exe|All files|*.*",
            Title = "Select rpcs3.exe",
            FileName = "rpcs3.exe",
        };
        if (dlg.ShowDialog() == true)
            Rpcs3PathBox.Text = dlg.FileName;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        var path = Rpcs3PathBox.Text.Trim();
        if (!File.Exists(path))
        {
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("WarnBrush");
            StatusText.Text = "Please select a valid rpcs3.exe.";
            return;
        }

        var cfg = App.Services.Config;
        cfg.Rpcs3Path = path;
        cfg.DeleteCacheAfterPlay = DeleteCacheCheck.IsChecked == true;
        cfg.EnableBackups = BackupsCheck.IsChecked == true;
        cfg.RunAtStartup = StartupCheck.IsChecked == true;
        cfg.SetupComplete = true;
        cfg.EnableRetailDecrypt = true;
        App.Services.SaveConfig();

        var exe = Environment.ProcessPath
            ?? Path.Combine(AppContext.BaseDirectory, "PES3-Disc.exe");
        if (cfg.RunAtStartup)
            StartupShortcut.Install(exe);
        else
            StartupShortcut.Remove();

        Pes3Log.Write("Setup completed.");
        DialogResult = true;
        Close();
    }
}
