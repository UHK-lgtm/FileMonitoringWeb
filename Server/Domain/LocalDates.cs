namespace FileMonitoringWeb.Server.Domain;

/// Calendar dates in the solution's business time zone. Pure.
public static class LocalDates
{
    public const string Luxembourg = "Europe/Luxembourg";

    /// The local calendar date in the given time zone right now.
    public static DateOnly Today(string timeZoneId) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)));
}
