using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Domain;

/// Works out which configuration fields changed, for the audit trail (spec section 20). Pure.
public static class AuditDiff
{
    /// Field names match the API contract, so the audit view and the export read the same names.
    private static readonly (string Name, Func<MonitoringDefinitionDto, object?> Value)[] AuditedFields =
    [
        ("name", d => d.Name),
        ("provider", d => d.Provider),
        ("relativePath", d => d.RelativePath),
        ("fileNamePattern", d => d.FileNamePattern),
        ("expectedFileCount", d => d.ExpectedFileCount),
        ("frequency", d => d.Frequency),
        ("isoWeekday", d => d.IsoWeekday),
        ("intervalDays", d => d.IntervalDays),
        ("anchorDate", d => d.AnchorDate),
        ("dayOfMonth", d => d.DayOfMonth),
        ("monthOfQuarter", d => d.MonthOfQuarter),
        ("expectedDeliveryTime", d => d.ExpectedDeliveryTime),
        ("bufferMinutes", d => d.BufferMinutes),
        ("timeZoneId", d => d.TimeZoneId),
        ("filenameDateMode", d => d.FilenameDateMode),
        ("filenameDateFormat", d => d.FilenameDateFormat),
        ("sizeCheckEnabled", d => d.SizeCheckEnabled),
        ("referenceSizeBytes", d => d.ReferenceSizeBytes),
        ("sizeTolerancePercent", d => d.SizeTolerancePercent),
        ("isEnabled", d => d.IsEnabled),
        ("isRetired", d => d.IsRetired),
    ];

    /// One change per field whose value is different. Unchanged fields produce nothing.
    public static IReadOnlyList<AuditChange> Diff(MonitoringDefinitionDto before, MonitoringDefinitionDto after)
    {
        var changes = new List<AuditChange>();
        foreach (var (name, value) in AuditedFields)
        {
            var oldValue = Format(value(before));
            var newValue = Format(value(after));
            if (oldValue != newValue)
                changes.Add(new AuditChange(name, oldValue, newValue));
        }

        return changes;
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        bool flag => flag ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
