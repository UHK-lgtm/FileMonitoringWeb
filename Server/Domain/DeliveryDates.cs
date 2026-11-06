namespace FileMonitoringWeb.Server.Domain;

/// Decides which dated rows belong to one expected delivery. Pure: no SQL and no HTTP types.
/// Uses the same window as FileScan (docs/ISSUES.md, D-1 and D-22): from the expected date up to,
/// but not including, the day after the deadline's local date, in the definition's time zone.
public static class DeliveryDates
{
    /// True if the instant falls on a local date inside the delivery's window.
    public static bool InWindow(DateTime utc, DateOnly expectedDate, DateTime deadlineUtc, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
        var windowEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(deadlineUtc, DateTimeKind.Utc), zone)).AddDays(1);

        return localDate >= expectedDate && localDate < windowEnd;
    }
}
