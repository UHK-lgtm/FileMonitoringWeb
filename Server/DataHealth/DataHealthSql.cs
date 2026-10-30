namespace FileMonitoringWeb.Server.DataHealth;

/// Builds the count queries for an approved source. Names are validated and quoted here; the date range is passed as
/// parameters @From and @ToExclusive. The only numbers placed in the text are convert styles from the allowed list.
public static class DataHealthSql
{
    /// Counts per day, for a real date column (uses the date column directly, so an index can be used).
    public static string CountDates(string schema, string obj, string column) =>
        $"""
        SELECT CAST(d.{Q(column)} AS date) AS Day, COUNT(*) AS Cnt
        FROM {Table(schema, obj)} AS d
        WHERE d.{Q(column)} >= @From AND d.{Q(column)} < @ToExclusive
        GROUP BY CAST(d.{Q(column)} AS date);
        """;

    /// Counts per day, for a text date column. Each row is read with the configured styles, in order.
    public static string CountText(string schema, string obj, string column, IReadOnlyList<int> styles) =>
        $"""
        SELECT x.Day, COUNT(*) AS Cnt
        FROM (SELECT {DayExpression(column, styles)} AS Day FROM {Table(schema, obj)} AS d) AS x
        WHERE x.Day >= @From AND x.Day < @ToExclusive
        GROUP BY x.Day;
        """;

    /// Rows that have text in the column but match none of the styles. Counted over the whole table.
    public static string UnreadableText(string schema, string obj, string column, IReadOnlyList<int> styles) =>
        $"""
        SELECT COUNT(*)
        FROM (SELECT d.{Q(column)} AS Raw, {DayExpression(column, styles)} AS Day FROM {Table(schema, obj)} AS d) AS x
        WHERE x.Raw IS NOT NULL AND x.Day IS NULL;
        """;

    /// The date for a text value: the first style that reads it, or NULL when none does.
    public static string DayExpression(string column, IReadOnlyList<int> styles)
    {
        var attempts = styles.Select(style => $"TRY_CONVERT(date, d.{Q(column)}, {style})").ToList();

        // COALESCE needs at least two arguments. A single style is used on its own.
        return attempts.Count == 1 ? attempts[0] : $"COALESCE({string.Join(", ", attempts)})";
    }

    private static string Table(string schema, string obj) => $"{Q(schema)}.{Q(obj)}";

    private static string Q(string name) => DataHealthRules.QuoteName(name);
}
