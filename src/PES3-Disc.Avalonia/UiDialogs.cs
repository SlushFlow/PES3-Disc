using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace PES3Disc.Avalonia;

public static class UiDialogs
{
    public static void ShowWarning(Window? owner, string message) =>
        ShowMessage(owner, "PES3-Disc", message, isInfo: false);

    public static void ShowInfo(Window? owner, string message) =>
        ShowMessage(owner, "PES3-Disc", message, isInfo: true);

    public static void ShowMessage(Window? owner, string title, string message, bool isInfo)
    {
        owner ??= GetDefaultOwner();
        var ok = new Button
        {
            Content = "OK",
            Classes = { "primary" },
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 96,
        };
        var dlg = new Window
        {
            Title = title,
            Width = 440,
            Height = 200,
            MinHeight = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#0A0E12"),
            Foreground = Brush.Parse("#F1F5F9"),
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brush.Parse("#F1F5F9"),
                    },
                    ok,
                },
            },
        };
        ok.Click += (_, _) => dlg.Close();
        if (owner is not null)
            dlg.ShowDialog(owner);
        else
            dlg.Show();
    }

    public static Window? GetDefaultOwner()
    {
        if (Application.Current?.ApplicationLifetime is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime d)
            return d.MainWindow;
        return null;
    }
}
