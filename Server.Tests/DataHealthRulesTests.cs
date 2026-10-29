using FileMonitoringWeb.Server.DataHealth;

namespace FileMonitoringWeb.Server.Tests;

public class DataHealthRulesTests
{
    // 29 October 2026 is a Thursday. 26 October is a Monday.
    private static readonly DateOnly Thursday = new(2026, 10, 29);
    private static readonly DateOnly Monday = new(2026, 10, 26);

    [Fact]
    public void Calendar_day_offset_counts_back_days()
    {
        Assert.Equal(new DateOnly(2026, 10, 27), DataHealthRules.ControlDate(Thursday, 2, DataHealthRules.CalendarDays));
    }

    [Fact]
    public void Weekday_offset_of_two_from_thursday_is_tuesday()
    {
        Assert.Equal(new DateOnly(2026, 10, 27), DataHealthRules.ControlDate(Thursday, 2, DataHealthRules.Weekdays));
    }

    [Fact]
    public void Weekday_offset_skips_the_weekend()
    {
        // Two weekdays before Monday 26 October is Thursday 22 October, skipping Saturday and Sunday.
        Assert.Equal(new DateOnly(2026, 10, 22), DataHealthRules.ControlDate(Monday, 2, DataHealthRules.Weekdays));
    }

    [Fact]
    public void Zero_offset_is_today()
    {
        Assert.Equal(Thursday, DataHealthRules.ControlDate(Thursday, 0, DataHealthRules.Weekdays));
    }

    [Fact]
    public void Negative_offset_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DataHealthRules.ControlDate(Thursday, -1, DataHealthRules.Weekdays));
    }

    [Fact]
    public void Unknown_offset_type_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => DataHealthRules.ControlDate(Thursday, 1, "Hours"));
    }

    [Fact]
    public void Percent_change_is_relative_to_the_previous_count()
    {
        Assert.Equal(-10m, DataHealthRules.PercentChange(90, 100));
        Assert.Equal(25m, DataHealthRules.PercentChange(125, 100));
    }

    [Fact]
    public void Percent_change_is_null_when_previous_count_is_zero()
    {
        Assert.Null(DataHealthRules.PercentChange(5, 0));
    }

    [Fact]
    public void No_rows_on_the_control_date_is_missing()
    {
        Assert.Equal(ControlStatus.Missing, DataHealthRules.Evaluate(0, 100));
    }

    [Fact]
    public void No_earlier_data_is_no_history()
    {
        Assert.Equal(ControlStatus.NoHistory, DataHealthRules.Evaluate(50, null));
        Assert.Equal(ControlStatus.NoHistory, DataHealthRules.Evaluate(50, 0));
    }

    [Fact]
    public void Lower_count_is_decrease_with_no_tolerance()
    {
        Assert.Equal(ControlStatus.Decrease, DataHealthRules.Evaluate(99, 100));
    }

    [Fact]
    public void Higher_count_is_increase()
    {
        Assert.Equal(ControlStatus.Increase, DataHealthRules.Evaluate(101, 100));
    }

    [Fact]
    public void Equal_count_is_stable()
    {
        Assert.Equal(ControlStatus.Stable, DataHealthRules.Evaluate(100, 100));
    }

    [Fact]
    public void Drop_within_tolerance_is_stable()
    {
        Assert.Equal(ControlStatus.Stable, DataHealthRules.Evaluate(97, 100, tolerancePercent: 5));
    }

    [Fact]
    public void Drop_beyond_tolerance_is_decrease()
    {
        Assert.Equal(ControlStatus.Decrease, DataHealthRules.Evaluate(90, 100, tolerancePercent: 5));
    }

    [Fact]
    public void Missing_and_decrease_are_attention_only_when_their_check_is_on()
    {
        Assert.True(DataHealthRules.IsAttention(ControlStatus.Missing, missingCheck: true, decreaseCheck: false));
        Assert.False(DataHealthRules.IsAttention(ControlStatus.Missing, missingCheck: false, decreaseCheck: true));
        Assert.True(DataHealthRules.IsAttention(ControlStatus.Decrease, missingCheck: false, decreaseCheck: true));
        Assert.False(DataHealthRules.IsAttention(ControlStatus.Decrease, missingCheck: true, decreaseCheck: false));
    }

    [Fact]
    public void Increase_stable_and_no_history_are_never_attention()
    {
        Assert.False(DataHealthRules.IsAttention(ControlStatus.Increase, true, true));
        Assert.False(DataHealthRules.IsAttention(ControlStatus.Stable, true, true));
        Assert.False(DataHealthRules.IsAttention(ControlStatus.NoHistory, true, true));
    }
}
