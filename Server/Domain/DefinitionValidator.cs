using System.Globalization;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Domain;

/// Checks a definition before it is saved. Mirrors the CHECK constraints in FileMonitoringWeb/sql/002, so bad input
/// gets a clear message instead of a database error. Pure: no SQL and no Azure types.
public static class DefinitionValidator
{
    public const string DateToken = "{date}";

    private static readonly HashSet<string> Frequencies = new(StringComparer.Ordinal) { "Daily", "Weekly", "EveryNDays", "Monthly", "Quarterly" };
    private static readonly HashSet<string> DateModes = new(StringComparer.Ordinal) { "NoDate", "SameDay", "PreviousDay", "MonthYear" };

    public static IReadOnlyList<string> Validate(DefinitionInput input)
    {
        var errors = new List<string>();

        ValidateText(input.Name, "name", 200, required: true, errors);
        ValidateText(input.Provider, "provider", 100, required: false, errors);
        ValidateRelativePath(input.RelativePath, errors);
        ValidateFileNamePattern(input.FileNamePattern, errors);

        if (input.ExpectedFileCount < 1)
            errors.Add("expectedFileCount must be at least 1.");

        ValidateSchedule(input, errors);
        ValidateDeliveryTime(input.ExpectedDeliveryTime, errors);

        if (input.BufferMinutes is < 0 or > 1440)
            errors.Add("bufferMinutes must be between 0 and 1440.");

        ValidateTimeZone(input.TimeZoneId, errors);
        ValidateFilenameDate(input, errors);
        ValidateSizeCheck(input, errors);

        return errors;
    }

    private static void ValidateText(string? value, string field, int maxLength, bool required, List<string> errors)
    {
        var text = value?.Trim() ?? string.Empty;
        if (required && text.Length == 0)
            errors.Add($"{field} is required.");
        else if (text.Length > maxLength)
            errors.Add($"{field} must be at most {maxLength} characters.");
    }

    /// Same rules as the scanner: relative to the share root, '/' separators, no drive letters, no '.' or '..' segments.
    private static void ValidateRelativePath(string? value, List<string> errors)
    {
        var path = value?.Trim() ?? string.Empty;
        if (path.Length == 0)
            return;

        if (path.StartsWith('/') || path.EndsWith('/') || path.Contains('\\') || path.Contains(':'))
        {
            errors.Add("relativePath must be relative, use '/' and must not contain ':'.");
            return;
        }

        if (path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            errors.Add("relativePath must not contain empty, '.' or '..' segments.");
    }

    private static void ValidateFileNamePattern(string? value, List<string> errors)
    {
        var pattern = value?.Trim() ?? string.Empty;
        if (pattern.Length == 0)
            errors.Add("fileNamePattern is required.");
        else if (pattern.Length > 260)
            errors.Add("fileNamePattern must be at most 260 characters.");
        else if (pattern.Contains('/') || pattern.Contains('\\'))
            errors.Add("fileNamePattern must be a file name only, without '/' or '\\'.");
    }

    private static void ValidateSchedule(DefinitionInput input, List<string> errors)
    {
        if (input.Frequency is null || !Frequencies.Contains(input.Frequency))
        {
            errors.Add("frequency must be one of Daily, Weekly, EveryNDays, Monthly, Quarterly.");
            return;
        }

        var needsWeekday = input.Frequency == "Weekly";
        var needsInterval = input.Frequency == "EveryNDays";
        var needsDayOfMonth = input.Frequency is "Monthly" or "Quarterly";
        var needsMonthOfQuarter = input.Frequency == "Quarterly";

        if (needsWeekday != (input.IsoWeekday is not null))
            errors.Add(needsWeekday ? "isoWeekday is required for Weekly." : "isoWeekday must be empty for this frequency.");
        else if (needsWeekday && input.IsoWeekday is not (>= 1 and <= 7))
            errors.Add("isoWeekday must be between 1 (Monday) and 7 (Sunday).");

        if (needsInterval != (input.IntervalDays is not null) || needsInterval != (input.AnchorDate is not null))
            errors.Add(needsInterval
                ? "intervalDays and anchorDate are required for EveryNDays."
                : "intervalDays and anchorDate must be empty for this frequency.");
        else if (needsInterval && input.IntervalDays < 1)
            errors.Add("intervalDays must be at least 1.");

        if (needsDayOfMonth != (input.DayOfMonth is not null))
            errors.Add(needsDayOfMonth ? "dayOfMonth is required for Monthly and Quarterly." : "dayOfMonth must be empty for this frequency.");
        else if (needsDayOfMonth && input.DayOfMonth is not (>= 1 and <= 31))
            errors.Add("dayOfMonth must be between 1 and 31.");

        if (needsMonthOfQuarter != (input.MonthOfQuarter is not null))
            errors.Add(needsMonthOfQuarter ? "monthOfQuarter is required for Quarterly." : "monthOfQuarter must be empty for this frequency.");
        else if (needsMonthOfQuarter && input.MonthOfQuarter is not (>= 1 and <= 3))
            errors.Add("monthOfQuarter must be between 1 and 3.");
    }

    private static void ValidateDeliveryTime(string? value, List<string> errors)
    {
        if (!TimeOnly.TryParseExact(value ?? string.Empty, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            errors.Add("expectedDeliveryTime must be a time in HH:mm format.");
    }

    private static void ValidateTimeZone(string? value, List<string> errors)
    {
        var zoneId = value?.Trim() ?? string.Empty;
        if (zoneId.Length == 0)
        {
            errors.Add("timeZoneId is required.");
            return;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            errors.Add("timeZoneId must be a valid IANA time zone, such as Europe/Luxembourg.");
        }
        catch (InvalidTimeZoneException)
        {
            errors.Add("timeZoneId must be a valid IANA time zone, such as Europe/Luxembourg.");
        }
    }

    private static void ValidateFilenameDate(DefinitionInput input, List<string> errors)
    {
        if (input.FilenameDateMode is null || !DateModes.Contains(input.FilenameDateMode))
        {
            errors.Add("filenameDateMode must be one of NoDate, SameDay, PreviousDay, MonthYear.");
            return;
        }

        var pattern = input.FileNamePattern ?? string.Empty;
        var hasToken = pattern.Contains(DateToken, StringComparison.Ordinal);

        if (input.FilenameDateMode == "NoDate")
        {
            if (hasToken)
                errors.Add("fileNamePattern must not contain {date} when filenameDateMode is NoDate.");
            if (!string.IsNullOrWhiteSpace(input.FilenameDateFormat))
                errors.Add("filenameDateFormat must be empty when filenameDateMode is NoDate.");
            return;
        }

        if (!hasToken)
            errors.Add("fileNamePattern must contain {date} when the filename date mode is not NoDate.");

        var format = input.FilenameDateFormat?.Trim() ?? string.Empty;
        if (format.Length == 0)
        {
            errors.Add("filenameDateFormat is required when the filename date mode is not NoDate.");
            return;
        }

        try
        {
            _ = DateOnly.FromDateTime(DateTime.UtcNow).ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            errors.Add("filenameDateFormat is not a valid date format, such as yyyyMMdd.");
        }
    }

    private static void ValidateSizeCheck(DefinitionInput input, List<string> errors)
    {
        if (!input.SizeCheckEnabled)
            return;

        if (input.ReferenceSizeBytes is null or <= 0)
            errors.Add("referenceSizeBytes must be above 0 when the size check is enabled.");
        if (input.SizeTolerancePercent is null or < 0)
            errors.Add("sizeTolerancePercent must be 0 or more when the size check is enabled.");
    }
}
