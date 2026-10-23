using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Tests;

public class AuditDiffTests
{
    private static MonitoringDefinitionDto Definition(string pattern = "LU_PO_FIO_20260112.xlsx", bool enabled = true) =>
        new(Id: 1,
            Name: "BNP Fund Accounting Test",
            Provider: "BNP",
            RelativePath: "BNP_FUND_ACCOUNTING/IN/PRD/test",
            FileNamePattern: pattern,
            ExpectedFileCount: 1,
            Frequency: "Daily",
            IsoWeekday: null,
            IntervalDays: null,
            AnchorDate: null,
            DayOfMonth: null,
            MonthOfQuarter: null,
            ExpectedDeliveryTime: "09:00",
            BufferMinutes: 0,
            TimeZoneId: "Europe/Luxembourg",
            FilenameDateMode: "NoDate",
            FilenameDateFormat: null,
            SizeCheckEnabled: false,
            ReferenceSizeBytes: null,
            SizeTolerancePercent: null,
            IsEnabled: enabled,
            IsRetired: false,
            ConcurrencyToken: "AAAAAAAAB9E=");

    [Fact]
    public void Identical_definitions_produce_no_changes()
    {
        Assert.Empty(AuditDiff.Diff(Definition(), Definition()));
    }

    [Fact]
    public void Only_the_changed_field_is_recorded_with_old_and_new_values()
    {
        var changes = AuditDiff.Diff(Definition(), Definition(pattern: "LU_PO_FIO_*.xlsx"));

        var change = Assert.Single(changes);
        Assert.Equal("fileNamePattern", change.FieldName);
        Assert.Equal("LU_PO_FIO_20260112.xlsx", change.OldValue);
        Assert.Equal("LU_PO_FIO_*.xlsx", change.NewValue);
    }

    [Fact]
    public void Booleans_are_recorded_as_true_and_false()
    {
        var change = Assert.Single(AuditDiff.Diff(Definition(enabled: true), Definition(enabled: false)));

        Assert.Equal("isEnabled", change.FieldName);
        Assert.Equal("true", change.OldValue);
        Assert.Equal("false", change.NewValue);
    }

    [Fact]
    public void Empty_to_value_is_recorded_with_null_old_value()
    {
        var change = Assert.Single(AuditDiff.Diff(Definition() with { FilenameDateFormat = null },
            Definition() with { FilenameDateFormat = "yyyyMMdd" }));

        Assert.Equal("filenameDateFormat", change.FieldName);
        Assert.Null(change.OldValue);
        Assert.Equal("yyyyMMdd", change.NewValue);
    }

    [Fact]
    public void Dates_are_recorded_in_iso_format()
    {
        var before = Definition() with { Frequency = "EveryNDays", IntervalDays = 15, AnchorDate = new DateOnly(2026, 1, 1) };
        var after = before with { AnchorDate = new DateOnly(2026, 2, 1) };

        var change = Assert.Single(AuditDiff.Diff(before, after));

        Assert.Equal("anchorDate", change.FieldName);
        Assert.Equal("2026-01-01", change.OldValue);
        Assert.Equal("2026-02-01", change.NewValue);
    }
}
