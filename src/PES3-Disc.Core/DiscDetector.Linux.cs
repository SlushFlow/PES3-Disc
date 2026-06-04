namespace PES3Disc.Core;

public static partial class DiscDetector
{
    private static IReadOnlyList<OpticalDrive> GetLinuxOpticalDrives()
    {
        var list = new List<OpticalDrive>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void TryAdd(string root, string? deviceNode, string? label)
        {
            root = NormalizeRoot(root);
            if (!seen.Add(root))
                return;
            try
            {
                if (!Directory.Exists(root))
                    return;
            }
            catch
            {
                return;
            }

            var display = label ?? Path.GetFileName(root.TrimEnd('/')) ?? root;
            var index = list.Count;
            var stableId = BuildLinuxVolumeId(root, deviceNode, display);
            list.Add(new OpticalDrive
            {
                Letter = (char)('A' + (index % 26)),
                Root = root,
                Id = stableId,
                VolumeLabel = label,
                DeviceNode = deviceNode,
            });
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || string.IsNullOrEmpty(drive.Name))
                    continue;
                if (drive.DriveType == DriveType.CDRom || LooksLikeDiscMount(drive.Name))
                    TryAdd(drive.Name, null, drive.VolumeLabel);
            }
            catch
            {
                // ignore
            }
        }

        if (File.Exists("/proc/mounts"))
        {
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;
                var dev = parts[0];
                var mount = parts[1].Replace("\\040", " ");
                var fs = parts[2];
                if (!IsDiscDevice(dev) && !IsDiscFilesystem(fs))
                    continue;
                if (mount.StartsWith("/media", StringComparison.Ordinal) ||
                    mount.StartsWith("/run/media", StringComparison.Ordinal) ||
                    mount.StartsWith("/mnt", StringComparison.Ordinal))
                {
                    TryAdd(mount, dev, Path.GetFileName(mount));
                }
            }
        }

        foreach (var baseDir in new[] { "/media", "/run/media" })
        {
            if (!Directory.Exists(baseDir))
                continue;
            foreach (var userDir in Directory.EnumerateDirectories(baseDir))
            {
                try
                {
                    foreach (var mount in Directory.EnumerateDirectories(userDir))
                        TryAdd(mount, null, Path.GetFileName(mount));
                }
                catch
                {
                    // ignore permission errors
                }
            }
        }

        return list;
    }

    private static bool LooksLikeDiscMount(string root)
    {
        root = NormalizeRoot(root);
        return File.Exists(Path.Combine(root, "PS3_DISC.SFB")) ||
               Directory.Exists(Path.Combine(root, "PS3_GAME"));
    }

    private static bool IsDiscDevice(string dev) =>
        dev.StartsWith("/dev/sr", StringComparison.Ordinal) ||
        dev.StartsWith("/dev/cdrom", StringComparison.Ordinal);

    private static bool IsDiscFilesystem(string fs) =>
        fs.Contains("udf", StringComparison.OrdinalIgnoreCase) ||
        fs.Contains("iso9660", StringComparison.OrdinalIgnoreCase);

    private static string BuildLinuxVolumeId(string root, string? deviceNode, string display)
    {
        var key = !string.IsNullOrWhiteSpace(deviceNode)
            ? deviceNode.Trim()
            : root.TrimEnd('/');
        return $"linux|{key}|{display}";
    }

    private static string NormalizeRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            return root;
        root = root.Replace('\\', '/');
        if (!root.EndsWith('/'))
            root += "/";
        return root;
    }
}
