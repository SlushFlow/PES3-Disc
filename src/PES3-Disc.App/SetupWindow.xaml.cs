using System.Windows;
using System.Windows.Controls;
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
        PopulateStorageModes(Pes3StorageModeResolver.Resolve(cfg));
        BackupsCheck.IsChecked = cfg.EnableBackups;
        StartupCheck.IsChecked = cfg.RunAtStartup;

        var found = App.Services.Launcher.FindRpcs3();
        if (string.IsNullOrEmpty(cfg.Rpcs3Path) && found is not null)
            Rpcs3PathBox.Text = found;
    }

    private void PopulateStorageModes(Pes3StorageMode current)
    {
        var items = new[]
        {
            (Pes3StorageMode.SmartHybrid, "Smart hybrid (recommended)"),
            (Pes3StorageMode.EphemeralSession, "Ephemeral session (delete after play)"),
        };
        StorageModeCombo.ItemsSource = items.Select(t => new ComboBoxItem
        {
            Content = t.Item2,
            Tag = t.Item1,
        }).ToList();
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
        StorageModeHint.Text = Pes3StorageModeResolver.Describe(
            StorageModeCombo.SelectedItem is ComboBoxItem { Tag: Pes3StorageMode mode } ? mode : Pes3StorageMode.SmartHybrid);
        StorageModeCombo.SelectionChanged += (_, _) =>
        {
            if (StorageModeCombo.SelectedItem is ComboBoxItem { Tag: Pes3StorageMode mode })
                StorageModeHint.Text = Pes3StorageModeResolver.Describe(mode);
        };
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

    private void OpenLegal_Click(object sender, RoutedEventArgs e) =>
        LegalTerms.TryOpenDocument("LEGAL.md");

    private void Finish_Click(object sender, RoutedEventArgs e)
    {
        var path = Rpcs3PathBox.Text.Trim();
        if (!File.Exists(path))
        {
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("WarnBrush");
            StatusText.Text = "Please select a valid rpcs3.exe.";
            return;
        }

        if (LegalOwnCheck.IsChecked != true || LegalNoShareCheck.IsChecked != true || LegalComplyCheck.IsChecked != true)
        {
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("WarnBrush");
            StatusText.Text = "Please confirm all legal statements to continue.";
            return;
        }

        var cfg = App.Services.Config;
        cfg.Rpcs3Path = path;
        if (StorageModeCombo.SelectedItem is ComboBoxItem { Tag: Pes3StorageMode mode })
            Pes3StorageModeResolver.Apply(cfg, mode);
        cfg.EnableBackups = BackupsCheck.IsChecked == true;
        cfg.RunAtStartup = StartupCheck.IsChecked == true;
        cfg.SetupComplete = true;
        cfg.EnableRetailDecrypt = true;
        LegalTerms.RecordAcceptance(cfg);
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
