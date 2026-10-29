namespace FileMonitoringWeb.Server.DataHealth;

/// Checks a Data Health control before it is saved. Pure: no SQL and no Azure types.
public static class DataHealthValidator
{
    public static IReadOnlyList<string> Validate(DataHealthControlInput input)
    {
        var errors = new List<string>();

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
            errors.Add("name is required.");
        else if (name.Length > 200)
            errors.Add("name must be at most 200 characters.");

        if (input.DisplayOrder < 0)
            errors.Add("displayOrder cannot be negative.");

        if (input.ApprovedSourceId <= 0)
            errors.Add("approvedSourceId must choose an approved source.");

        if (input.OffsetDays is < 0 or > 60)
            errors.Add("offsetDays must be between 0 and 60.");

        if (input.OffsetType is not ("CalendarDays" or "Weekdays"))
            errors.Add("offsetType must be CalendarDays or Weekdays.");

        if (input.HistoryDays is < 1 or > 365)
            errors.Add("historyDays must be between 1 and 365.");

        if (input.DecreaseTolerancePercent is < 0 or > 100)
            errors.Add("decreaseTolerancePercent must be between 0 and 100.");

        return errors;
    }
}
