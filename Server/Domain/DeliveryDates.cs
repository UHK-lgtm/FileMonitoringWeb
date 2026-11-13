namespace FileMonitoringWeb.Server.Domain;

/// Decides which dated rows belong to one expected delivery. Pure: no SQL and no HTTP types.
/// Uses the same window as FileScan (docs/ISSUES.md, D-1, D-22 and D-23): the expected date in the definition's
/// time zone, from 00:00 up to midnight at the end of that day. The deadline never falls after that midnight.
public static class DeliveryDates
{
    /// True if the instant falls inside the delivery's window. Midnight at the end of the day is included,
    /// so a Missing or Incomplete change recorded at a midnight deadline is shown.
    public static bool InWindow(DateTime utc, DateOnly expectedDate, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(expectedDate.ToDateTime(TimeOnly.MinValue), zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(expectedDate.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return instant >= startUtc && instant <= endUtc;
    }
}
