namespace FileMonitoringWeb.Server.DataHealth;

/// An approved source the web app may count. The list is maintained by an admin script; the web app only reads it.
public sealed record ApprovedSourceDto(
    int Id,
    string SourceKey,
    string DisplayName,
    string SchemaName,
    string ObjectName,
    string DateColumn,
    string DateColumnType,
    string? DateStyles,
    string ConcurrencyToken);

/// The body of PUT for a source's date format. Only the format can be changed from the web app.
public sealed record DataHealthSourceFormatInput(
    string? DateColumnType,
    string? DateStyles,
    string? ConcurrencyToken);

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
    bool IsRetired,
    string ConcurrencyToken);

/// The body of POST and PUT for a control. The server sets the Id, retirement and the audit values.
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
