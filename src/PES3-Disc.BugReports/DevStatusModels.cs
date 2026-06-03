namespace PES3Disc.BugReports;

public sealed class DevStatusResponse
{
    public string Effective { get; set; } = "grey";
    public string Mode { get; set; } = "auto";
    public string Label { get; set; } = "";
    public bool IsAutoSchedule { get; set; } = true;
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DevStatusUpdateRequest
{
    /// <summary>auto | green | yellow | grey</summary>
    public string Mode { get; set; } = "auto";
}
