using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Tests;

public class DefinitionValidatorTests
{
    /// A valid daily definition with no date in the file name. Individual tests change one field at a time.
    private static DefinitionInput Valid() =>
        new(Name: "BNP Fund Accounting Test",
            Provider: "BNP",
            RelativePath: "BNP_FUND_ACCOUNTING/IN/PRD/test",
            FileNamePattern: "LU_PO_FIO_20260112.xlsx",
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
            IsEnabled: true,
            ConcurrencyToken: null);

    [Fact]
    public void Valid_daily_definition_has_no_errors()
    {
        Assert.Empty(DefinitionValidator.Validate(Valid()));
    }

    [Fact]
    public void Name_is_required()
    {
        var errors = DefinitionValidator.Validate(Valid() with { Name = "   " });

        Assert.Contains("name is required.", errors);
    }

    [Theory]
    [InlineData("/leading")]
    [InlineData("trailing/")]
    [InlineData("back\\slash")]
    [InlineData("C:drive")]
    [InlineData("a//b")]
    [InlineData("a/../b")]
    public void Relative_path_must_be_relative_and_clean(string path)
    {
        var errors = DefinitionValidator.Validate(Valid() with { RelativePath = path });

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void Empty_relative_path_means_the_share_root()
    {
        Assert.Empty(DefinitionValidator.Validate(Valid() with { RelativePath = "" }));
    }

    [Fact]
    public void File_name_pattern_cannot_contain_folders()
    {
        var errors = DefinitionValidator.Validate(Valid() with { FileNamePattern = "folder/file.xlsx" });

        Assert.Contains("fileNamePattern must be a file name only, without '/' or '\\'.", errors);
    }

    [Fact]
    public void Weekly_requires_an_iso_weekday()
    {
        var errors = DefinitionValidator.Validate(Valid() with { Frequency = "Weekly" });

        Assert.Contains("isoWeekday is required for Weekly.", errors);
    }

    [Fact]
    public void Daily_rejects_a_weekday()
    {
        var errors = DefinitionValidator.Validate(Valid() with { IsoWeekday = 1 });

        Assert.Contains("isoWeekday must be empty for this frequency.", errors);
    }

    [Fact]
    public void Every_n_days_requires_interval_and_anchor()
    {
        var errors = DefinitionValidator.Validate(Valid() with { Frequency = "EveryNDays" });

        Assert.Contains("intervalDays and anchorDate are required for EveryNDays.", errors);
    }

    [Fact]
    public void Quarterly_requires_day_and_month_of_quarter()
    {
        var errors = DefinitionValidator.Validate(Valid() with { Frequency = "Quarterly" });

        Assert.Contains("dayOfMonth is required for Monthly and Quarterly.", errors);
        Assert.Contains("monthOfQuarter is required for Quarterly.", errors);
    }

    [Fact]
    public void Month_of_quarter_must_be_one_to_three()
    {
        var errors = DefinitionValidator.Validate(Valid() with { Frequency = "Quarterly", DayOfMonth = 10, MonthOfQuarter = 4 });

        Assert.Contains("monthOfQuarter must be between 1 and 3.", errors);
    }

    [Fact]
    public void Delivery_time_must_be_hh_mm()
    {
        var errors = DefinitionValidator.Validate(Valid() with { ExpectedDeliveryTime = "9am" });

        Assert.Contains("expectedDeliveryTime must be a time in HH:mm format.", errors);
    }

    [Fact]
    public void Buffer_must_be_within_a_day()
    {
        var errors = DefinitionValidator.Validate(Valid() with { BufferMinutes = 1441 });

        Assert.Contains("bufferMinutes must be between 0 and 1440.", errors);
    }

    [Fact]
    public void Unknown_time_zone_is_rejected()
    {
        var errors = DefinitionValidator.Validate(Valid() with { TimeZoneId = "Mars/Olympus" });

        Assert.Contains("timeZoneId must be a valid IANA time zone, such as Europe/Luxembourg.", errors);
    }

    [Fact]
    public void Date_mode_needs_the_date_token_and_a_format()
    {
        var errors = DefinitionValidator.Validate(Valid() with { FilenameDateMode = "SameDay" });

        Assert.Contains("fileNamePattern must contain {date} when the filename date mode is not NoDate.", errors);
        Assert.Contains("filenameDateFormat is required when the filename date mode is not NoDate.", errors);
    }

    [Fact]
    public void Date_token_with_no_date_mode_is_rejected()
    {
        var errors = DefinitionValidator.Validate(Valid() with { FileNamePattern = "LU_{date}.xlsx" });

        Assert.Contains("fileNamePattern must not contain {date} when filenameDateMode is NoDate.", errors);
    }

    [Fact]
    public void Same_day_with_token_and_format_is_valid()
    {
        var errors = DefinitionValidator.Validate(Valid() with
        {
            FileNamePattern = "LU_PO_FIO_{date}.xlsx",
            FilenameDateMode = "SameDay",
            FilenameDateFormat = "yyyyMMdd",
        });

        Assert.Empty(errors);
    }

    [Fact]
    public void Size_check_needs_a_reference_and_tolerance()
    {
        var errors = DefinitionValidator.Validate(Valid() with { SizeCheckEnabled = true });

        Assert.Contains("referenceSizeBytes must be above 0 when the size check is enabled.", errors);
        Assert.Contains("sizeTolerancePercent must be 0 or more when the size check is enabled.", errors);
    }
}
