namespace PES3Disc.BugReports;

/// <summary>Resolves developer availability for PES3-Disc status UI.</summary>
public static class DevStatusLogic
{
    public const int WorkStartHourEt = 8;
    public const int WorkEndHourEt = 22;

    public static DevStatusKind GetScheduledStatus(DateTime utcNow)
    {
        var et = ToEastern(utcNow);
        var hour = et.Hour;
        return hour is >= WorkStartHourEt and < WorkEndHourEt
            ? DevStatusKind.Green
            : DevStatusKind.Grey;
    }

    public static DevStatusKind ResolveEffective(string? manualMode, DateTime utcNow)
    {
        var mode = (manualMode ?? "auto").Trim().ToLowerInvariant();
        if (mode == "auto")
            return GetScheduledStatus(utcNow);

        return DevStatusKindExtensions.TryParse(mode, out var kind)
            ? kind
            : GetScheduledStatus(utcNow);
    }

    public static bool IsAutoMode(string? manualMode) =>
        string.IsNullOrWhiteSpace(manualMode)
        || string.Equals(manualMode.Trim(), "auto", StringComparison.OrdinalIgnoreCase);

    public static DevStatusResponse BuildDisplay(string manualMode, DateTime? updatedAtUtc = null)
    {
        var effective = ResolveEffective(manualMode, DateTime.UtcNow);
        return new DevStatusResponse
        {
            Effective = effective.ToApiValue(),
            Mode = (manualMode ?? "auto").Trim().ToLowerInvariant(),
            Label = GetLabel(effective),
            IsAutoSchedule = IsAutoMode(manualMode),
            UpdatedAtUtc = updatedAtUtc,
        };
    }

    /// <summary>Time until green/grey flips when mode is auto (8 AM / 10 PM Eastern).</summary>
    public static TimeSpan GetDelayUntilNextBoundary(DateTime utcNow)
    {
        var et = ToEastern(utcNow);
        DateTime nextEt;
        if (et.Hour < WorkStartHourEt)
            nextEt = et.Date.AddHours(WorkStartHourEt);
        else if (et.Hour < WorkEndHourEt)
            nextEt = et.Date.AddHours(WorkEndHourEt);
        else
            nextEt = et.Date.AddDays(1).AddHours(WorkStartHourEt);

        var nextUtc = ToUtcFromEastern(nextEt);
        var delay = nextUtc - utcNow;
        if (delay < TimeSpan.FromSeconds(1))
            delay = TimeSpan.FromSeconds(1);
        if (delay > TimeSpan.FromDays(1))
            delay = TimeSpan.FromMinutes(1);
        return delay;
    }

    public static string GetLabel(DevStatusKind kind) => kind switch
    {
        DevStatusKind.Green => "At home and working",
        DevStatusKind.Yellow => "Taking a break or on vacation",
        DevStatusKind.Grey => "Nighttime / day off",
        _ => "Unavailable",
    };

    public static string GetDotColor(DevStatusKind kind) => kind switch
    {
        DevStatusKind.Green => "#3DDC84",
        DevStatusKind.Yellow => "#F5C542",
        DevStatusKind.Grey => "#8B95A8",
        _ => "#8B95A8",
    };

    public static DateTime ToEastern(DateTime utcNow)
    {
        var utc = utcNow.Kind == DateTimeKind.Utc
            ? utcNow
            : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

        var tz = TryGetEasternTimeZone();
        return tz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(utc, tz)
            : utc.AddHours(-5);
    }

    private static DateTime ToUtcFromEastern(DateTime easternUnspecified)
    {
        var tz = TryGetEasternTimeZone();
        var local = DateTime.SpecifyKind(easternUnspecified, DateTimeKind.Unspecified);
        return tz is not null
            ? TimeZoneInfo.ConvertTimeToUtc(local, tz)
            : local.AddHours(5);
    }

    private static TimeZoneInfo? TryGetEasternTimeZone()
    {
        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // try next
            }
            catch (InvalidTimeZoneException)
            {
                // try next
            }
        }

        return null;
    }
}
