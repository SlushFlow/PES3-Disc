using System.Windows;
using System.Windows.Controls;
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
        PopulateStorageModes(Pes3StorageModeResolver.Resolve(c));
        CachePathBox.Text = c.DumpCachePath;
        RetailCheck.IsChecked = c.EnableRetailDecrypt;
        UnknownCheck.IsChecked = c.DecryptUnknownOpticalMedia;
        DumpCliBox.Text = c.DumpCliPath;
        IrdDirBox.Text = c.IrdDir;
        BackupsCheck.IsChecked = c.EnableBackups;
        BackupOnLaunchCheck.IsChecked = c.BackupOnLaunch;
        StartupCheck.IsChecked = c.RunAtStartup;
        DelayBox.Text = c.ScanDelaySeconds.ToString();
        EjectCleanupCheck.IsChecked = c.CleanupSessionsOnDiscEject;
        OverlayMbBox.Text = c.OverlayMaxLocalMegabytes.ToString();
        BugReportApiBox.Text = c.BugReportApiUrl;
        RefreshLegalStatus();
    }

    private void RefreshLegalStatus()
    {
        var c = App.Services.Config;
        LegalStatusText.Text = LegalTerms.IsAccepted(c)
            ? $"Terms accepted ({LegalTerms.CurrentVersion}) on {c.LegalTermsAcceptedUtc:u}."
            : "Decrypt and copy require accepting the current terms.";
    }

    private void OpenLegal_Click(object sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("LEGAL.md");

    private void ReviewLegal_Click(object sender, RoutedEventArgs e)
    {
        LegalTerms.ClearAcceptance(App.Services.Config);
        App.Services.SaveConfig();
        if (LegalTermsWindow.Prompt(this))
            RefreshLegalStatus();
    }

    private void PopulateStorageModes(Pes3StorageMode current)
    {
        var items = new[]
        {
            (Pes3StorageMode.SmartHybrid, "Smart hybrid (recommended)"),
            (Pes3StorageMode.PersistentLibrary, "Persistent library"),
            (Pes3StorageMode.EphemeralSession, "Ephemeral session"),
            (Pes3StorageMode.DiscDirect, "Play DIY from disc (no copy)"),
        };
        StorageModeCombo.ItemsSource = items.Select(t => new ComboBoxItem
        {
            Content = t.Item2,
            Tag = t.Item1,
        }).ToList();
        StorageModeCombo.DisplayMemberPath = "Content";
        for (var i = 0; i < StorageModeCombo.Items.Count; i++)
        {
            if (StorageModeCombo.Items[i] is ComboBoxItem { Tag: Pes3StorageMode m } && m == current)
            {
                StorageModeCombo.SelectedIndex = i;
                break;
            }
        }
        if (StorageModeCombo.SelectedIndex < 0)
            StorageModeCombo.SelectedIndex = 0;
        StorageModeCombo.SelectionChanged += (_, _) =>
        {
            if (StorageModeCombo.SelectedItem is ComboBoxItem { Tag: Pes3StorageMode mode })
                StorageModeHint.Text = Pes3StorageModeResolver.Describe(mode);
        };
        StorageModeHint.Text = Pes3StorageModeResolver.Describe(current);
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
        if (StorageModeCombo.SelectedItem is ComboBoxItem { Tag: Pes3StorageMode mode })
            Pes3StorageModeResolver.Apply(c, mode);
        c.DumpCachePath = CachePathBox.Text.Trim();
        c.EnableRetailDecrypt = RetailCheck.IsChecked == true;
        c.DecryptUnknownOpticalMedia = UnknownCheck.IsChecked == true;
        c.DumpCliPath = DumpCliBox.Text.Trim();
        c.IrdDir = IrdDirBox.Text.Trim();
        c.EnableBackups = BackupsCheck.IsChecked == true;
        c.BackupOnLaunch = BackupOnLaunchCheck.IsChecked == true;
        c.RunAtStartup = StartupCheck.IsChecked == true;
        c.ScanDelaySeconds = delay;
        c.CleanupSessionsOnDiscEject = EjectCleanupCheck.IsChecked == true;
        if (int.TryParse(OverlayMbBox.Text.Trim(), out var overlayMb))
            c.OverlayMaxLocalMegabytes = Math.Clamp(overlayMb, 64, 32_768);
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
