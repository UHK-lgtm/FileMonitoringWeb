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

    /// SQL Server convert styles a text date column may use. Only these numbers are ever placed into SQL text.
    public static readonly IReadOnlySet<int> AllowedStyles = new HashSet<int>
    {
        23, 101, 102, 103, 104, 105, 106, 107, 110, 111, 112, 113, 114, 120, 121, 126,
    };

    public static readonly IReadOnlySet<string> DateColumnTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "date", "datetime", "datetime2", "smalldatetime", "datetimeoffset",
    };

    public static readonly IReadOnlySet<string> TextColumnTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "varchar", "nvarchar", "char", "nchar",
    };

    public static bool IsTextType(string? type) => type is not null && TextColumnTypes.Contains(type);

    public static bool IsAllowedColumnType(string? type) =>
        type is not null && (DateColumnTypes.Contains(type) || TextColumnTypes.Contains(type));

    /// Reads "112,23,103" into the styles in that order. Null when the list is empty, has an unknown style, or repeats one.
    public static IReadOnlyList<int>? ParseStyles(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var styles = new List<int>();
        foreach (var part in value.Split(','))
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var style)
                || !AllowedStyles.Contains(style)
                || styles.Contains(style))
                return null;

            styles.Add(style);
        }

        return styles;
    }

    /// The stored form of a control's style list: the numbers in order, with no spaces. Null for a date type, and null for
    /// a text type whose list is not valid.
    public static string? NormalizeStyles(string? dateColumnType, string? value) =>
        IsTextType(dateColumnType) && ParseStyles(value) is { } styles ? string.Join(",", styles) : null;

    /// A table, view, schema or column name as it appears in the catalog. Spaces and punctuation are allowed, because the
    /// name is always quoted, and the catalog check must find the exact name before anything runs.
    public static bool IsValidName(string? name) =>
        name is { Length: > 0 and <= 128 } && name.All(character => !char.IsControl(character));

    /// Brackets a name for SQL. A ']' inside the name is doubled, so the name cannot close the bracket early.
    public static string QuoteName(string name) => "[" + name.Replace("]", "]]", StringComparison.Ordinal) + "]";

    /// Only Missing and Decrease are attention conditions. A check that is switched off does not raise attention.
    public static bool IsAttention(ControlStatus status, bool missingCheck, bool decreaseCheck) =>
        status switch
        {
            ControlStatus.Missing => missingCheck,
            ControlStatus.Decrease => decreaseCheck,
            _ => false,
        };
}
