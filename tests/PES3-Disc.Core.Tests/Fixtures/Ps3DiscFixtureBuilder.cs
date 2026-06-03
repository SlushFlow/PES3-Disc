using System.Text;

namespace PES3Disc.Core.Tests.Fixtures;

/// <summary>Cross-platform builder for DIY and retail PS3 disc test layouts.</summary>
public static class Ps3DiscFixtureBuilder
{
    public const string DiyTitleId = "BLUS99991";
    public const string RetailTitleId = "BLUS99992";
    public const string DiyTitle = "PES3 DIY Test Disc";
    public const string RetailTitle = "PES3 Retail Test Disc";

    public static string DiyFixtureName => "diy-demo-disc";
    public static string RetailFixtureName => "retail-encrypted-disc";

    public static void WriteStandardFixtures(string repoRoot)
    {
        WriteDiyDisc(Path.Combine(repoRoot, "test-fixtures", DiyFixtureName));
        WriteRetailDisc(Path.Combine(repoRoot, "test-fixtures", RetailFixtureName));
    }

    public static string WriteDiyDisc(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "PS3_GAME", "USRDIR"));
        WriteDecryptedEboot(Path.Combine(root, "PS3_GAME", "USRDIR", "EBOOT.BIN"));
        WriteParamSfo(Path.Combine(root, "PS3_GAME", "PARAM.SFO"), DiyTitleId, DiyTitle);
        File.WriteAllText(Path.Combine(root, "PS3_DISC.SFB"), "PES3-DIY-TEST", Encoding.ASCII);
        File.WriteAllText(
            Path.Combine(root, "PS3_GAME", "USRDIR", "readme.txt"),
            "Temporary DIY test layout for PES3-Disc.",
            Encoding.UTF8);
        return root;
    }

    public static string WriteRetailDisc(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "PS3_GAME", "USRDIR"));
        WriteEncryptedEboot(Path.Combine(root, "PS3_GAME", "USRDIR", "EBOOT.BIN"));
        WriteParamSfo(Path.Combine(root, "PS3_GAME", "PARAM.SFO"), RetailTitleId, RetailTitle, discId: "PES3-RETAIL-TEST");
        File.WriteAllText(Path.Combine(root, "PS3_DISC.SFB"), "PES3-RETAIL-TEST", Encoding.ASCII);
        Directory.CreateDirectory(Path.Combine(root, "BDMV", "STREAM"));
        return root;
    }

    public static string WriteIncompleteDisc(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "PS3_GAME", "USRDIR"));
        WriteParamSfo(Path.Combine(root, "PS3_GAME", "PARAM.SFO"), "BLUS99993", "Incomplete Test");
        return root;
    }

    public static string WriteNestedDiyDisc(string root)
    {
        var gameDir = Path.Combine(root, "Nested Game Title");
        WriteDiyDisc(gameDir);
        return root;
    }

    public static string WriteDevBdvdDisc(string root)
    {
        var ps3 = Path.Combine(root, "dev_bdvd", "PS3_GAME", "USRDIR");
        Directory.CreateDirectory(ps3);
        WriteDecryptedEboot(Path.Combine(ps3, "EBOOT.BIN"));
        WriteParamSfo(Path.Combine(root, "dev_bdvd", "PS3_GAME", "PARAM.SFO"), "BLUS99994", "dev_bdvd Test");
        return root;
    }

    public static void WriteDecryptedEboot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[]
        {
            0x7F, 0x45, 0x4C, 0x46, 0x01, 0x02, 0x01, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        });
    }

    public static void WriteEncryptedEboot(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[]
        {
            0x53, 0x43, 0x45, 0x00, 0x00, 0x00, 0x02, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        });
    }

    public static void WriteParamSfo(string path, string titleId, string title, string category = "GD", string discId = "PES3-TEST-DISC")
    {
        var entries = new (string Key, string Value)[]
        {
            ("TITLE", title),
            ("TITLE_ID", titleId),
            ("CATEGORY", category),
            ("DISC_ID", discId),
            ("PS3_SYSTEM_VER", "03.0000"),
        };

        const int headerSize = 0x14;
        var keyCount = entries.Length;
        var keyTableSize = keyCount * 16;

        var nameBytes = new List<byte>();
        var nameOffsets = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, _) in entries)
        {
            nameOffsets[key] = headerSize + keyTableSize + nameBytes.Count;
            nameBytes.AddRange(Encoding.ASCII.GetBytes(key));
            nameBytes.Add(0);
        }

        while (nameBytes.Count % 4 != 0)
            nameBytes.Add(0);

        var dataTableOffset = headerSize + keyTableSize + nameBytes.Count;
        var dataBytes = new List<byte>();
        var keyTable = new List<byte>();

        foreach (var (key, value) in entries)
        {
            var valueBytes = Encoding.UTF8.GetBytes(value);
            var dataOff = dataBytes.Count;
            dataBytes.AddRange(valueBytes);
            dataBytes.Add(0);
            while (dataBytes.Count % 4 != 0)
                dataBytes.Add(0);

            var entry = new byte[16];
            BitConverter.GetBytes((ushort)nameOffsets[key]).CopyTo(entry, 0);
            BitConverter.GetBytes((ushort)0x0204).CopyTo(entry, 2);
            var len = (uint)(valueBytes.Length + 1);
            BitConverter.GetBytes(len).CopyTo(entry, 4);
            BitConverter.GetBytes(len).CopyTo(entry, 8);
            BitConverter.GetBytes((uint)dataOff).CopyTo(entry, 12);
            keyTable.AddRange(entry);
        }

        var file = new List<byte> { 0, 0x50, 0x53, 0x46 };
        file.AddRange(BitConverter.GetBytes(0x101u));
        file.AddRange(BitConverter.GetBytes((uint)keyCount));
        file.AddRange(BitConverter.GetBytes((uint)headerSize));
        file.AddRange(BitConverter.GetBytes((uint)dataTableOffset));
        file.AddRange(keyTable);
        file.AddRange(nameBytes);
        file.AddRange(dataBytes);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, file.ToArray());
    }

    public static string? FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (Directory.Exists(Path.Combine(dir, "test-fixtures"))
                && File.Exists(Path.Combine(dir, "PES3-Disc.sln")))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }

        return null;
    }
}
