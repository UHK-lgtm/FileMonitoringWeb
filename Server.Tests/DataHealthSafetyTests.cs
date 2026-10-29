using FileMonitoringWeb.Server.DataHealth;

namespace FileMonitoringWeb.Server.Tests;

public class DataHealthSafetyTests
{
    [Theory]
    [InlineData("Positions")]
    [InlineData("dbo_Positions_2026")]
    [InlineData("_hidden")]
    public void Plain_names_are_safe(string name)
    {
        Assert.True(DataHealthRules.IsSafeIdentifier(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1Positions")]
    [InlineData("Positions; DROP TABLE x")]
    [InlineData("[Positions]")]
    [InlineData("dbo.Positions")]
    [InlineData("Pos ition")]
    [InlineData("Positions--")]
    [InlineData("Posi'tions")]
    public void Names_that_could_change_the_query_are_rejected(string name)
    {
        Assert.False(DataHealthRules.IsSafeIdentifier(name));
    }

    [Fact]
    public void Null_name_is_rejected()
    {
        Assert.False(DataHealthRules.IsSafeIdentifier(null));
    }

    [Fact]
    public void Previous_available_date_is_the_latest_earlier_day_with_data()
    {
        var counts = new Dictionary<DateOnly, int>
        {
            [new DateOnly(2026, 10, 20)] = 5,
            [new DateOnly(2026, 10, 22)] = 9,
            [new DateOnly(2026, 10, 23)] = 0,
            [new DateOnly(2026, 10, 27)] = 4,
        };

        Assert.Equal(new DateOnly(2026, 10, 22), DataHealthRules.PreviousAvailableDate(new DateOnly(2026, 10, 27), counts));
    }

    [Fact]
    public void Previous_available_date_is_null_when_nothing_is_earlier()
    {
        var counts = new Dictionary<DateOnly, int> { [new DateOnly(2026, 10, 27)] = 4 };

        Assert.Null(DataHealthRules.PreviousAvailableDate(new DateOnly(2026, 10, 27), counts));
    }

    [Fact]
    public void Validator_accepts_a_sensible_control()
    {
        var input = Input();

        Assert.Empty(DataHealthValidator.Validate(input));
    }

    [Fact]
    public void Validator_rejects_out_of_range_values()
    {
        var errors = DataHealthValidator.Validate(Input() with
        {
            OffsetDays = 61,
            HistoryDays = 0,
            DecreaseTolerancePercent = 101,
            OffsetType = "Hours",
            ApprovedSourceId = 0,
        });

        Assert.Equal(5, errors.Count);
    }

    [Fact]
    public void Validator_requires_a_name()
    {
        Assert.Contains("name is required.", DataHealthValidator.Validate(Input() with { Name = "  " }));
    }

    [Fact]
    public void Audit_records_only_changed_control_fields()
    {
        var before = Control();
        var after = before with { HistoryDays = 30, IsEnabled = false, ApprovedSourceId = 7 };

        var changes = ControlAuditDiff.Diff(before, after);

        Assert.Equal(3, changes.Count);
        Assert.Contains(changes, c => c.FieldName == "historyDays" && c.OldValue == "10" && c.NewValue == "30");
        Assert.Contains(changes, c => c.FieldName == "isEnabled" && c.OldValue == "true" && c.NewValue == "false");
        Assert.Contains(changes, c => c.FieldName == "approvedSourceId" && c.NewValue == "7");
    }

    private static DataHealthControlInput Input() =>
        new(Name: "Positions received",
            DisplayOrder: 1,
            IsEnabled: true,
            ApprovedSourceId: 3,
            OffsetDays: 2,
            OffsetType: "Weekdays",
            HistoryDays: 10,
            MissingCheck: true,
            DecreaseCheck: true,
            DecreaseTolerancePercent: 0,
            ConcurrencyToken: null);

    private static DataHealthControlDto Control() =>
        new(Id: 1,
            Name: "Positions received",
            DisplayOrder: 1,
            IsEnabled: true,
            ApprovedSourceId: 3,
            SourceKey: "positions",
            SourceName: "Positions",
            OffsetDays: 2,
            OffsetType: "Weekdays",
            HistoryDays: 10,
            MissingCheck: true,
            DecreaseCheck: true,
            DecreaseTolerancePercent: 0,
            IsRetired: false,
            ConcurrencyToken: "AAAAAAAAB9E=");
}
