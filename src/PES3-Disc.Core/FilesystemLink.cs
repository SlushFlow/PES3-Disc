using System.Diagnostics;

namespace PES3Disc.Core;

public static class FilesystemLink
{
    public static bool TryCreateFileLink(string linkPath, string targetPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(linkPath) || Directory.Exists(linkPath))
                return true;

            var targetFull = Path.GetFullPath(targetPath);
            File.CreateSymbolicLink(linkPath, targetFull);
            return File.Exists(linkPath) || new FileInfo(linkPath).Exists;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            var parent = Path.GetDirectoryName(linkPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            if (Directory.Exists(linkPath) || File.Exists(linkPath))
                return true;

            var targetFull = Path.GetFullPath(targetPath);
            if (!Directory.Exists(targetFull))
                return false;

            if (OperatingSystem.IsWindows())
                return TryCreateWindowsDirectoryJunction(linkPath, targetFull);

            Directory.CreateSymbolicLink(linkPath, targetFull);
            return Directory.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCreateWindowsDirectoryJunction(string linkPath, string targetFull)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetFull);
            if (Directory.Exists(linkPath))
                return true;
        }
        catch
        {
            // fall through to mklink
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{linkPath}\" \"{targetFull}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi);
            if (proc is null)
                return false;
            proc.WaitForExit(15_000);
            return proc.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch
        {
            return false;
        }
    }
}
