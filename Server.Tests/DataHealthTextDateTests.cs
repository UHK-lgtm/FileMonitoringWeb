using FileMonitoringWeb.Server.DataHealth;

namespace FileMonitoringWeb.Server.Tests;

// These tests need no database. They check the rules and the statements the app builds for a text date column,
// such as yyyyMMdd text ('20261028') read with style 112. They cannot prove that SQL Server converts a value;
// that is checked by running the statement against a database.
public class DataHealthTextDateTests
{
    [Fact]
    public void Yyyymmdd_text_is_read_with_style_112()
    {
        Assert.Equal(new[] { 112 }, DataHealthRules.ParseStyles("112"));
    }

    [Theory]
    [InlineData("varchar")]
    [InlineData("VARCHAR")]
    [InlineData("nvarchar")]
    [InlineData("char")]
    [InlineData("nchar")]
    public void Text_types_are_recognised_whatever_the_case(string type)
    {
        Assert.True(DataHealthRules.IsTextType(type));
    }

    [Theory]
    [InlineData("date")]
    [InlineData("datetime")]
    [InlineData("datetime2")]
    public void Date_types_are_not_text(string type)
    {
        Assert.False(DataHealthRules.IsTextType(type));
    }

    [Fact]
    public void Column_named_ReportDate_is_a_valid_name()
    {
        Assert.True(DataHealthRules.IsValidName("ReportDate"));
        Assert.True(DataHealthRules.IsValidName("CITCO_Position"));
    }

    [Fact]
    public void Count_statement_for_CITCO_ReportDate_reads_each_row_with_style_112()
    {
        var sql = DataHealthSql.CountText("Import", "CITCO_Position", "ReportDate", new[] { 112 });

        Assert.Contains("FROM [Import].[CITCO_Position] AS d", sql);
        Assert.Contains("COALESCE(TRY_CONVERT(date, d.[ReportDate], 112))", sql);
        Assert.Contains("WHERE x.Day >= @From AND x.Day < @ToExclusive", sql);
        Assert.Contains("GROUP BY x.Day", sql);
    }

    [Fact]
    public void Unreadable_statement_for_CITCO_ReportDate_counts_text_with_no_date()
    {
        var sql = DataHealthSql.UnreadableText("Import", "CITCO_Position", "ReportDate", new[] { 112 });

        Assert.Contains("FROM [Import].[CITCO_Position] AS d", sql);
        Assert.Contains("d.[ReportDate] AS Raw", sql);
        Assert.Contains("x.Raw IS NOT NULL AND x.Day IS NULL", sql);
    }

    [Fact]
    public void Several_styles_are_tried_in_the_order_given()
    {
        var sql = DataHealthSql.DayExpression("ReportDate", new[] { 112, 23 });

        Assert.Equal("COALESCE(TRY_CONVERT(date, d.[ReportDate], 112), TRY_CONVERT(date, d.[ReportDate], 23))", sql);
    }

    [Fact]
    public void Style_list_with_spaces_is_rejected_by_the_server_parser()
    {
        // The web page sends "112,23" with no spaces. A direct API call with spaces is refused, and the database
        // check constraint refuses them too.
        Assert.Null(DataHealthRules.ParseStyles("112, 23"));
    }
}
