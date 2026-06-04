namespace PES3Disc.Core;

/// <summary>How PES3-Disc stores and reuses game files between sessions.</summary>
public enum Pes3StorageMode
{
    /// <summary>Library when available; DIY may boot from disc without copying; retail decrypts into library.</summary>
    SmartHybrid,
    /// <summary>Keep decrypted/copied titles under the PES3 library for fast replay.</summary>
    PersistentLibrary,
    /// <summary>Temp session folders removed when RPCS3 exits (saves disk, slow repeat).</summary>
    EphemeralSession,
    /// <summary>DIY decrypted discs: boot from disc/mount path (zero copy; disc must stay inserted).</summary>
    DiscDirect,
}

public static class Pes3StorageModeResolver
{
    public static Pes3StorageMode Resolve(Pes3Config config)
    {
        if (!string.IsNullOrWhiteSpace(config.StorageMode))
        {
            if (TryParse(config.StorageMode, out var parsed))
                return parsed;
        }

        return config.DeleteCacheAfterPlay
            ? Pes3StorageMode.EphemeralSession
            : Pes3StorageMode.SmartHybrid;
    }

    public static bool TryParse(string? value, out Pes3StorageMode mode)
    {
        mode = Pes3StorageMode.SmartHybrid;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var key = value.Trim().Replace("-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.OrdinalIgnoreCase);

        mode = key.ToUpperInvariant() switch
        {
            "SMART" or "SMARTHYBRID" or "HYBRID" or "DEFAULT" => Pes3StorageMode.SmartHybrid,
            "LIBRARY" or "PERSISTENT" or "PERSISTENTLIBRARY" or "CACHE" => Pes3StorageMode.PersistentLibrary,
            "SESSION" or "EPHEMERAL" or "EPHEMERALSESSION" or "TEMP" => Pes3StorageMode.EphemeralSession,
            "DISC" or "DISCDIRECT" or "DIRECT" or "PLAYFROMDISC" => Pes3StorageMode.DiscDirect,
            _ => Pes3StorageMode.SmartHybrid,
        };

        return key.Length > 0;
    }

    public static void SyncLegacyFlags(Pes3Config config)
    {
        var mode = Resolve(config);
        config.DeleteCacheAfterPlay = mode == Pes3StorageMode.EphemeralSession;
        config.StorageMode = mode switch
        {
            Pes3StorageMode.SmartHybrid => "SmartHybrid",
            Pes3StorageMode.PersistentLibrary => "PersistentLibrary",
            Pes3StorageMode.EphemeralSession => "EphemeralSession",
            Pes3StorageMode.DiscDirect => "DiscDirect",
            _ => "SmartHybrid",
        };
    }

    public static void Apply(Pes3Config config, Pes3StorageMode mode)
    {
        config.StorageMode = mode switch
        {
            Pes3StorageMode.SmartHybrid => "SmartHybrid",
            Pes3StorageMode.PersistentLibrary => "PersistentLibrary",
            Pes3StorageMode.EphemeralSession => "EphemeralSession",
            Pes3StorageMode.DiscDirect => "DiscDirect",
            _ => "SmartHybrid",
        };
        config.DeleteCacheAfterPlay = mode == Pes3StorageMode.EphemeralSession;
    }

    /// <summary>True when decrypted/copied titles are kept under library/titles for replay.</summary>
    public static bool KeepsPersistentLibrary(Pes3Config config) =>
        Resolve(config) == Pes3StorageMode.PersistentLibrary;

    public static bool UsesPersistentLibrary(Pes3Config config) =>
        KeepsPersistentLibrary(config);

    public static bool AllowsDiscDirect(Pes3Config config)
    {
        var mode = Resolve(config);
        return mode is Pes3StorageMode.SmartHybrid or Pes3StorageMode.DiscDirect;
    }

    public static string Describe(Pes3StorageMode mode) => mode switch
    {
        Pes3StorageMode.SmartHybrid =>
            "Smart: small SSD session + disc provides bulk; removed when RPCS3 exits or you eject the disc. Retail decrypts per session unless in library.",
        Pes3StorageMode.PersistentLibrary =>
            "Library: keep full game trees on SSD for instant replay (most disk, fastest repeat).",
        Pes3StorageMode.EphemeralSession =>
            "Session: temp folders deleted when RPCS3 exits (least disk, slowest repeat).",
        Pes3StorageMode.DiscDirect =>
            "Disc: DIY boots from the disc/mount with no copy (zero disk; keep disc inserted).",
        _ => "",
    };
}

public enum Pes3LibraryTier
{
    EphemeralSession,
    PersistentLibrary,
    DiscReference,
    DiscAssistedOverlay,
}
