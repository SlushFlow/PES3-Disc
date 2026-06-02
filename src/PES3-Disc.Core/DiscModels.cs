namespace PES3Disc.Core;

public enum DiscVolumeKind
{
    NoPs3Layout,
    Playable,
    EncryptedRetail,
    IncompleteBurn,
}

public sealed class OpticalDrive
{
    /// <summary>Display index (Windows drive letter or A/B/C on Linux).</summary>
    public required char Letter { get; init; }
    public required string Root { get; init; }
    public required string Id { get; init; }
    public string? VolumeLabel { get; init; }
    /// <summary>Linux block device (e.g. /dev/sr0) when known.</summary>
    public string? DeviceNode { get; init; }

    public string DisplayName =>
        VolumeLabel ?? (OperatingSystem.IsWindows() ? $"{Letter}:" : Root.TrimEnd('/', '\\'));
}

public sealed class DetectedGame
{
    public required string EbootPath { get; init; }
    public string? Title { get; init; }
    public string? TitleId { get; init; }
    /// <summary>Parent folder of PS3_GAME (disc root or game folder on volume).</summary>
    public string? GameRoot { get; init; }
}

public sealed class DiscVolumeStatus
{
    public required DiscVolumeKind Kind { get; init; }
    public required string Message { get; init; }
    public DetectedGame? Game { get; init; }
}

public sealed class DecryptResult
{
    public bool Success { get; init; }
    public string? ProductCode { get; init; }
    public string? Title { get; init; }
    public string? GameRoot { get; init; }
    public string? Eboot { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class DecryptProgress
{
    public string? Phase { get; init; }
    public int CurrentFile { get; init; }
    public int TotalFiles { get; init; }
    public long ProcessedSectors { get; init; }
    public long TotalFileSectors { get; init; }
    public string? ProductCode { get; init; }
    public string? Title { get; init; }

    public int Percent =>
        TotalFileSectors > 0
            ? (int)Math.Clamp(100 * ProcessedSectors / TotalFileSectors, 0, 100)
            : 0;
}
