namespace FileMonitoringWeb.Server.Models;

/// One row of FileScan.MonitoringDefinition, as returned by the API (docs/api-contract.md).
public sealed record MonitoringDefinitionDto(
    int Id,
    string Name,
    string Provider,
    string RelativePath,
    string FileNamePattern,
    int ExpectedFileCount,
    string Frequency,
    int? IsoWeekday,
    int? IntervalDays,
    DateOnly? AnchorDate,
    int? DayOfMonth,
    int? MonthOfQuarter,
    string ExpectedDeliveryTime,
    int BufferMinutes,
    string TimeZoneId,
    string FilenameDateMode,
    string? FilenameDateFormat,
    bool SizeCheckEnabled,
    long? ReferenceSizeBytes,
    decimal? SizeTolerancePercent,
    bool IsEnabled,
    bool IsRetired,
    string ConcurrencyToken)
{
    /// Builds the DTO that an update would produce from the input. Id, retirement and the token come from the stored row.
    public static MonitoringDefinitionDto FromInput(int id, DefinitionInput input, bool isRetired, string concurrencyToken) =>
        new(id,
            input.Name!.Trim(),
            (input.Provider ?? string.Empty).Trim(),
            (input.RelativePath ?? string.Empty).Trim(),
            input.FileNamePattern!.Trim(),
            input.ExpectedFileCount,
            input.Frequency!,
            input.IsoWeekday,
            input.IntervalDays,
            input.AnchorDate,
            input.DayOfMonth,
            input.MonthOfQuarter,
            input.ExpectedDeliveryTime!,
            input.BufferMinutes,
            input.TimeZoneId!.Trim(),
            input.FilenameDateMode!,
            input.FilenameDateFormat?.Trim(),
            input.SizeCheckEnabled,
            input.ReferenceSizeBytes,
            input.SizeTolerancePercent,
            input.IsEnabled,
            isRetired,
            concurrencyToken);
}

/// The body of POST and PUT. Strings are checked by DefinitionValidator before they reach SQL.
public sealed record DefinitionInput(
    string? Name,
    string? Provider,
    string? RelativePath,
    string? FileNamePattern,
    int ExpectedFileCount,
    string? Frequency,
    int? IsoWeekday,
    int? IntervalDays,
    DateOnly? AnchorDate,
    int? DayOfMonth,
    int? MonthOfQuarter,
    string? ExpectedDeliveryTime,
    int BufferMinutes,
    string? TimeZoneId,
    string? FilenameDateMode,
    string? FilenameDateFormat,
    bool SizeCheckEnabled,
    long? ReferenceSizeBytes,
    decimal? SizeTolerancePercent,
    bool IsEnabled,
    string? ConcurrencyToken);

/// One row of the configuration audit (spec section 20).
public sealed record AuditEntryDto(
    long AuditId,
    DateTime ChangedAtUtc,
    string ChangedBy,
    string FieldName,
    string? OldValue,
    string? NewValue);

/// One changed field, ready to be written to the audit table.
public sealed record AuditChange(string FieldName, string? OldValue, string? NewValue);
