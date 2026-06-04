namespace PES3Disc.Core;

public sealed class Pes3ConfigLoadResult
{
    public required Pes3Config Config { get; init; }
    public string? Warning { get; init; }
    public bool LoadedFromDisk { get; init; }
}
