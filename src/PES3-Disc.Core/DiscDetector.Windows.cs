namespace PES3Disc.Core;

public static partial class DiscDetector
{
    private static IReadOnlyList<OpticalDrive> GetWindowsOpticalDrives()
    {
        var list = new List<OpticalDrive>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.DriveType != DriveType.CDRom)
                continue;
            try
            {
                if (!drive.IsReady || string.IsNullOrEmpty(drive.Name))
                    continue;
            }
            catch
            {
                continue;
            }

            var letter = drive.Name[0];
            var id = !string.IsNullOrEmpty(drive.VolumeLabel)
                ? $"{letter}|{drive.VolumeLabel}"
                : $"{letter}|{drive.DriveFormat}";

            list.Add(new OpticalDrive
            {
                Letter = letter,
                Root = drive.Name,
                Id = id,
                VolumeLabel = drive.VolumeLabel,
                DeviceNode = null,
            });
        }

        return list;
    }
}
