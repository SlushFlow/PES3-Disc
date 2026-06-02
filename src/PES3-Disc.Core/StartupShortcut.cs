using System.Runtime.Versioning;

namespace PES3Disc.Core;

[SupportedOSPlatform("windows")]
public static class StartupShortcut
{
    private const string ShortcutName = "PES3-Disc.lnk";

    public static string ShortcutPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            ShortcutName);

    public static bool IsInstalled => File.Exists(ShortcutPath);

    public static void Install(string exePath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(ShortcutPath);
        shortcut.TargetPath = exePath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
        shortcut.WindowStyle = 7;
        shortcut.Description = "PES3-Disc — PS3 discs in RPCS3";
        shortcut.Save();
    }

    public static void Remove()
    {
        if (File.Exists(ShortcutPath))
            File.Delete(ShortcutPath);
    }
}
