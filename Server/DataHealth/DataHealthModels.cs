namespace FileMonitoringWeb.Server.DataHealth;

/// An approved source the web app may count. The list is maintained by an admin script; the web app only reads it.
/// The date format is not here: each control holds its own format.
public sealed record ApprovedSourceDto(
    int Id,
    string SourceKey,
    string DisplayName,
    string SchemaName,
    string ObjectName,
    string DateColumn);

/// One Data Health control, as returned by the configuration endpoints.
public sealed record DataHealthControlDto(
    int Id,
    string Name,
    int DisplayOrder,
    bool IsEnabled,
    int ApprovedSourceId,
    string SourceKey,
    string SourceName,
    int OffsetDays,
    string OffsetType,
    int HistoryDays,
    bool MissingCheck,
    bool DecreaseCheck,
    decimal DecreaseTolerancePercent,
    string DateColumnType,
    string? DateStyles,
    bool IsRetired,
    string ConcurrencyToken);

/// The body of POST and PUT for a control. The server sets the Id, retirement and the audit values.
/// DateColumnType and DateStyles describe how the source's date column is stored: a real date type, or a text type
/// with an ordered list of convert styles (for example 112 for yyyyMMdd).
public sealed record DataHealthControlInput(
    string? Name,
    int DisplayOrder,
    bool IsEnabled,
    int ApprovedSourceId,
    int OffsetDays,
    string? OffsetType,
    int HistoryDays,
    bool MissingCheck,
    bool DecreaseCheck,
    decimal DecreaseTolerancePercent,
    string? DateColumnType,
    string? DateStyles,
    string? ConcurrencyToken);

/// One day in a control's trend: the record count for that day.
public sealed record TrendPoint(DateOnly Date, int Count);

/// The result for one control on the Data Health page. Status is Error when the check could not run; the reason is never SQL text.
public sealed record ControlResultDto(
    int ControlId,
    string Name,
    string SourceName,
    DateOnly ControlDate,
    int ControlCount,
    DateOnly? ComparisonDate,
    int? PreviousCount,
    decimal? PercentChange,
    DateOnly? LatestVisibleDate,
    int? LatestVisibleCount,
    string Status,
    bool Attention,
    string? Message,
    IReadOnlyList<TrendPoint> Trend,
    int UnreadableCount = 0);

/// The whole Data Health page: summary and one result per enabled control, in display order.
public sealed record DataHealthResultsDto(
    DateOnly Today,
    DateTime RefreshedAtUtc,
    int EnabledSources,
    int MissingCount,
    int DecreaseCount,
    int ErrorCount,
    bool Healthy,
    IReadOnlyList<ControlResultDto> Controls);
