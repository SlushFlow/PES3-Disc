using PES3Disc.Core;

namespace PES3Disc.ViewModels;

public sealed class DiscCardModel
{
    public required OpticalDrive Drive { get; init; }
    public required DiscVolumeStatus Status { get; init; }
    public required string Title { get; init; }
    public required string Detail { get; init; }
    public bool CanPlay { get; init; }
    public bool CanPlayFromCache { get; init; }
    public bool CanDecrypt { get; init; }
    public bool CanDecryptAgain { get; init; }
    public bool DecryptAvailable { get; init; }
    public string PlayButtonText { get; init; } = "Play";
    public bool IsDismissed { get; set; }
    public CachedGameEntry? LibraryEntry { get; init; }
}
