using FileMonitoringWeb.Server.Export;

namespace FileMonitoringWeb.Server.Tests;

public class AuditWorkbookTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"x\")", "'=HYPERLINK(\"x\")")]
    [InlineData("+1", "'+1")]
    [InlineData("-1", "'-1")]
    [InlineData("@sum", "'@sum")]
    [InlineData("LU_PO_FIO_*.xlsx", "LU_PO_FIO_*.xlsx")]
    public void Values_that_start_with_formula_characters_are_neutralised(string value, string expected)
    {
        Assert.Equal(expected, AuditWorkbook.Safe(value));
    }
}
