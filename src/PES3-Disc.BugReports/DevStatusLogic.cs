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

        foreach (var id in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(id);
                return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
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

        return utc.AddHours(-5);
    }
}
