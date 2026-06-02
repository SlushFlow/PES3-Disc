namespace PES3Disc.Core;

/// <summary>Facade for platform-specific retail disc decryption backends.</summary>
public sealed class DiscDecryptor
{
    private readonly IDiscDumpBackend _backend;

    public DiscDecryptor(Pes3Config? config = null) => _backend = DiscDumpBackend.Create(config);

    public bool IsAvailable => _backend.IsAvailable;

    public static string? FindDumpCliPath(Pes3Config? config = null) =>
        (DiscDumpBackend.Create(config) as DiscDumpBackendBase)?.ExecutablePath;

    public Task<DecryptResult> DecryptAsync(
        char driveLetter,
        string outputBase,
        IProgress<DecryptProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _backend.DecryptAsync(
            new OpticalDrive
            {
                Letter = driveLetter,
                Root = $"{driveLetter}:\\",
                Id = $"win|{driveLetter}",
            },
            outputBase,
            progress,
            cancellationToken);

    public Task<DecryptResult> DecryptAsync(
        OpticalDrive drive,
        string outputBase,
        IProgress<DecryptProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        _backend.DecryptAsync(drive, outputBase, progress, cancellationToken);
}
