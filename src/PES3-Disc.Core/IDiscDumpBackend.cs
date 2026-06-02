namespace PES3Disc.Core;

public interface IDiscDumpBackend
{
    bool IsAvailable { get; }
    Task<DecryptResult> DecryptAsync(
        OpticalDrive drive,
        string outputBase,
        IProgress<DecryptProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public static class DiscDumpBackend
{
    public static IDiscDumpBackend Create(Pes3Config? config = null)
    {
        if (OperatingSystem.IsLinux())
            return new LinuxDiscDumpBackend(config);
        return new WindowsDiscDumpBackend(config);
    }
}
