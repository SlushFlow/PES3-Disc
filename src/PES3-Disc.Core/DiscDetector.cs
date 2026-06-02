namespace PES3Disc.Core;

public static class DiscDetector
{
    public static IReadOnlyList<OpticalDrive> GetOpticalDrives()
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
            });
        }

        return list;
    }

    public static DiscVolumeStatus GetVolumeStatus(string driveRoot)
    {
        if (!driveRoot.EndsWith(Path.DirectorySeparatorChar))
            driveRoot += Path.DirectorySeparatorChar;

        var eboot = FindEboot(driveRoot);
        var hasSfb = File.Exists(Path.Combine(driveRoot, "PS3_DISC.SFB"));
        var ps3Game = Path.Combine(driveRoot, "PS3_GAME");
        var hasParam = File.Exists(Path.Combine(ps3Game, "PARAM.SFO"));

        if (eboot is not null)
        {
            if (IsEncryptedEboot(eboot))
            {
                return new DiscVolumeStatus
                {
                    Kind = DiscVolumeKind.EncryptedRetail,
                    Message = "Encrypted retail disc — decryption required.",
                    Game = null,
                };
            }

            return new DiscVolumeStatus
            {
                Kind = DiscVolumeKind.Playable,
                Message = "Ready to play in RPCS3.",
                Game = GameFromEboot(eboot),
            };
        }

        if (Directory.Exists(ps3Game) && !hasParam)
        {
            return new DiscVolumeStatus
            {
                Kind = DiscVolumeKind.IncompleteBurn,
                Message = "PS3_GAME folder found but EBOOT.BIN is missing.",
                Game = null,
            };
        }

        if (hasParam || hasSfb)
        {
            return new DiscVolumeStatus
            {
                Kind = DiscVolumeKind.EncryptedRetail,
                Message = "Retail PS3 disc structure detected.",
                Game = null,
            };
        }

        return new DiscVolumeStatus
        {
            Kind = DiscVolumeKind.NoPs3Layout,
            Message = "No PS3 game layout on this volume.",
            Game = null,
        };
    }

    public static DetectedGame? FindGameOnDrive(string driveRoot)
    {
        var status = GetVolumeStatus(driveRoot);
        return status.Game;
    }

    private static string? FindEboot(string driveRoot)
    {
        var candidates = new[]
        {
            Path.Combine(driveRoot, "PS3_GAME", "USRDIR", "EBOOT.BIN"),
            Path.Combine(driveRoot, "dev_bdvd", "PS3_GAME", "USRDIR", "EBOOT.BIN"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(driveRoot).Take(48))
            {
                var nested = Path.Combine(dir, "PS3_GAME", "USRDIR", "EBOOT.BIN");
                if (File.Exists(nested))
                    return nested;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static bool IsEncryptedEboot(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> buf = stackalloc byte[7];
            if (fs.Read(buf) < 7)
                return false;
            return buf[0] == 0x53 && buf[1] == 0x43 && buf[2] == 0x45 && buf[6] == 2;
        }
        catch
        {
            return false;
        }
    }

    private static DetectedGame GameFromEboot(string eboot)
    {
        var ps3Game = Directory.GetParent(Directory.GetParent(eboot)!.FullName)!.FullName;
        var sfo = Path.Combine(ps3Game, "PARAM.SFO");
        var fields = ParamSfo.ReadFields(sfo);
        var titleId = fields.GetValueOrDefault("TITLE_ID") ?? Path.GetFileName(Directory.GetParent(ps3Game)!.FullName);
        var title = fields.GetValueOrDefault("TITLE") ?? titleId;

        return new DetectedGame
        {
            EbootPath = eboot,
            Title = title,
            TitleId = titleId,
        };
    }
}
