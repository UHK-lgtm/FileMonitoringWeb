using System.Globalization;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.DataHealth;

/// Works out which Data Health control fields changed, for the audit trail (spec section 9). Pure.
public static class ControlAuditDiff
{
    private static readonly (string Name, Func<DataHealthControlDto, object?> Value)[] AuditedFields =
    [
        ("name", c => c.Name),
        ("displayOrder", c => c.DisplayOrder),
        ("isEnabled", c => c.IsEnabled),
        ("approvedSourceId", c => c.ApprovedSourceId),
        ("offsetDays", c => c.OffsetDays),
        ("offsetType", c => c.OffsetType),
        ("historyDays", c => c.HistoryDays),
        ("missingCheck", c => c.MissingCheck),
        ("decreaseCheck", c => c.DecreaseCheck),
        ("decreaseTolerancePercent", c => c.DecreaseTolerancePercent),
        ("dateColumnType", c => c.DateColumnType),
        ("dateStyles", c => c.DateStyles),
        ("isRetired", c => c.IsRetired),
    ];

    /// One change per field whose value differs. Unchanged fields produce nothing.
    public static IReadOnlyList<AuditChange> Diff(DataHealthControlDto before, DataHealthControlDto after)
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
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
