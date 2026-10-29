namespace FileMonitoringWeb.Server.DataHealth;

/// Data Health statuses (docs/Data_Health_Basic_Specifications.md, section 3). "Error" is a display state only and is never stored.
public enum ControlStatus
{
    Missing,
    Decrease,
    Increase,
    Stable,
    NoHistory,
}

/// Control-date, percentage and status rules for Data Health. Pure: no SQL and no Azure types.
public static class DataHealthRules
{
    public const string CalendarDays = "CalendarDays";
    public const string Weekdays = "Weekdays";

    /// The control date: today minus the offset. Weekdays skip Saturday and Sunday (T-2 weekday in the spec).
    public static DateOnly ControlDate(DateOnly today, int offset, string offsetType)
    {
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative.");

        if (offsetType == CalendarDays)
            return today.AddDays(-offset);

        if (offsetType != Weekdays)
            throw new ArgumentException("offsetType must be CalendarDays or Weekdays.", nameof(offsetType));

        var date = today;
        var remaining = offset;
        while (remaining > 0)
        {
            date = date.AddDays(-1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                remaining--;
        }

        return date;
    }

    /// Percentage change from the previous count. Null when there is no previous count to compare with.
    public static decimal? PercentChange(int controlCount, int previousCount) =>
        previousCount == 0 ? null : Math.Round((controlCount - previousCount) * 100m / previousCount, 2);

    /// No rows on the control date is Missing. No earlier data is NoHistory. Otherwise the change is compared with the tolerance:
    /// a drop beyond the tolerance is Decrease, a rise beyond it is Increase, and anything within it is Stable.
    public static ControlStatus Evaluate(int controlCount, int? previousCount, decimal tolerancePercent = 0)
    {
        if (controlCount == 0)
            return ControlStatus.Missing;

        if (previousCount is null or 0)
            return ControlStatus.NoHistory;

        var change = PercentChange(controlCount, previousCount.Value)!.Value;
        if (change < -tolerancePercent)
            return ControlStatus.Decrease;

        if (change > tolerancePercent)
            return ControlStatus.Increase;

        return ControlStatus.Stable;
    }

    /// The comparison date: the latest day before the control date that has data. Null when there is none.
    public static DateOnly? PreviousAvailableDate(DateOnly controlDate, IReadOnlyDictionary<DateOnly, int> countsByDay) =>
        countsByDay
            .Where(entry => entry.Key < controlDate && entry.Value > 0)
            .Select(entry => (DateOnly?)entry.Key)
            .Max();

    /// A table, view or column name that is safe to place in SQL: letters, digits and underscores, starting with a letter or underscore.
    /// Names are also checked against the database catalog before use. Values are never built into SQL text.
    public static bool IsSafeIdentifier(string? name) =>
        name is not null && System.Text.RegularExpressions.Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,127}$");

    /// Only Missing and Decrease are attention conditions. A check that is switched off does not raise attention.
    public static bool IsAttention(ControlStatus status, bool missingCheck, bool decreaseCheck) =>
        status switch
        {
            ControlStatus.Missing => missingCheck,
            ControlStatus.Decrease => decreaseCheck,
            _ => false,
        };
}
