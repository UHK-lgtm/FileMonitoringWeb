using FileMonitoringWeb.Server.DataHealth;

namespace FileMonitoringWeb.Server.Tests;

public class DataHealthSafetyTests
{
    [Theory]
    [InlineData("Positions")]
    [InlineData("Trade Date")]
    [InlineData("dbo_Positions_2026")]
    [InlineData("Client (old)")]
    public void Names_with_letters_spaces_and_punctuation_are_valid(string name)
    {
        Assert.True(DataHealthRules.IsValidName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Trade\tDate")]
    [InlineData("Trade\nDate")]
    public void Empty_or_control_character_names_are_rejected(string name)
    {
        Assert.False(DataHealthRules.IsValidName(name));
    }

    [Fact]
    public void Names_longer_than_128_characters_are_rejected()
    {
        Assert.False(DataHealthRules.IsValidName(new string('a', 129)));
        Assert.True(DataHealthRules.IsValidName(new string('a', 128)));
    }

    [Theory]
    [InlineData("Trade Date", "[Trade Date]")]
    [InlineData("Positions", "[Positions]")]
    [InlineData("A]B", "[A]]B]")]
    [InlineData("x]; DROP TABLE y; --", "[x]]; DROP TABLE y; --]")]
    public void Names_are_quoted_and_closing_brackets_are_doubled(string name, string expected)
    {
        Assert.Equal(expected, DataHealthRules.QuoteName(name));
    }

    [Fact]
    public void Style_list_keeps_the_order_given()
    {
        Assert.Equal(new[] { 112, 23, 103 }, DataHealthRules.ParseStyles("112,23,103"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("112,")]
    [InlineData("112,,23")]
    [InlineData("999")]
    [InlineData("112,112")]
    [InlineData("112; DROP")]
    [InlineData("yyyyMMdd")]
    public void Invalid_style_lists_are_rejected(string value)
    {
        Assert.Null(DataHealthRules.ParseStyles(value));
    }

    [Fact]
    public void Stored_style_list_is_normalised_for_text_types_only()
    {
        Assert.Equal("112,23", DataHealthRules.NormalizeStyles("varchar", "112,23"));
        Assert.Null(DataHealthRules.NormalizeStyles("date", "112"));
        Assert.Null(DataHealthRules.NormalizeStyles("varchar", "112,112"));
    }

    [Fact]
    public void Text_count_tries_each_style_in_order_and_quotes_the_names()
    {
        var sql = DataHealthSql.CountText("FileScan", "Trade Feed", "Trade Date", new[] { 112, 23 });

        Assert.Contains("COALESCE(TRY_CONVERT(date, d.[Trade Date], 112), TRY_CONVERT(date, d.[Trade Date], 23))", sql);
        Assert.Contains("FROM [FileScan].[Trade Feed] AS d", sql);
        Assert.Contains("@From", sql);
        Assert.Contains("@ToExclusive", sql);
    }

    [Fact]
    public void Unreadable_query_counts_text_that_matches_no_style()
    {
        var sql = DataHealthSql.UnreadableText("dbo", "Positions", "PositionDate", new[] { 112 });

        Assert.Contains("x.Raw IS NOT NULL AND x.Day IS NULL", sql);
    }

    [Fact]
    public void Date_count_uses_the_column_directly_and_takes_no_styles()
    {
        var sql = DataHealthSql.CountDates("dbo", "Positions", "PositionDate");

        Assert.DoesNotContain("TRY_CONVERT", sql);
        Assert.Contains("d.[PositionDate] >= @From", sql);
    }

    [Fact]
    public void Format_accepts_text_with_styles_and_date_without()
    {
        Assert.Empty(DataHealthValidator.ValidateFormat("varchar", "112"));
        Assert.Empty(DataHealthValidator.ValidateFormat("datetime2", null));
    }

    [Fact]
    public void Format_rejects_text_without_styles_and_styles_on_a_date()
    {
        Assert.NotEmpty(DataHealthValidator.ValidateFormat("varchar", null));
        Assert.NotEmpty(DataHealthValidator.ValidateFormat("date", "112"));
    }

    [Fact]
    public void Format_rejects_an_unknown_column_type()
    {
        Assert.NotEmpty(DataHealthValidator.ValidateFormat("money", null));
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
    public void Validator_accepts_a_sensible_control()
    {
        Assert.Empty(DataHealthValidator.Validate(Input()));
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
    public void Validator_rejects_a_bad_format_on_the_control()
    {
        var errors = DataHealthValidator.Validate(Input() with { DateColumnType = "varchar", DateStyles = null });

        Assert.Single(errors);
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

    [Fact]
    public void Audit_records_a_change_of_format_on_the_control()
    {
        var before = Control();
        var after = before with { DateColumnType = "varchar", DateStyles = "112" };

        var changes = ControlAuditDiff.Diff(before, after);

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c.FieldName == "dateColumnType" && c.OldValue == "date" && c.NewValue == "varchar");
        Assert.Contains(changes, c => c.FieldName == "dateStyles" && c.OldValue == null && c.NewValue == "112");
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
            DateColumnType: "date",
            DateStyles: null,
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
            DateColumnType: "date",
            DateStyles: null,
            IsRetired: false,
            ConcurrencyToken: "AAAAAAAAB9E=");
}
