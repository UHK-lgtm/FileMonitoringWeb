using ClosedXML.Excel;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Export;

/// Builds the Excel audit export from saved SQL data only (spec section 23). Pure apart from the workbook.
public static class AuditWorkbook
{
    private static readonly string[] FormulaStarts = ["=", "+", "-", "@"];

    public static byte[] Build(
        IReadOnlyList<(int DefinitionId, string DefinitionName, AuditEntryDto Entry)> audit,
        IReadOnlyList<DeliveryListItemDto> deliveries)
    {
        using var workbook = new XLWorkbook();

        var auditSheet = workbook.Worksheets.Add("Configuration audit");
        WriteHeader(auditSheet, "Definition Id", "Definition", "Changed (UTC)", "Changed by", "Field", "Old value", "New value");
        var row = 2;
        foreach (var (definitionId, definitionName, entry) in audit)
        {
            WriteRow(auditSheet, row++,
                definitionId.ToString(),
                definitionName,
                entry.ChangedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                entry.ChangedBy,
                entry.FieldName,
                entry.OldValue ?? string.Empty,
                entry.NewValue ?? string.Empty);
        }

        var deliverySheet = workbook.Worksheets.Add("Deliveries");
        WriteHeader(deliverySheet, "Delivery Id", "Definition Id", "Definition", "Expected date", "Status",
            "Expected files", "Received files", "Unexpected files", "Deadline (UTC)", "Completed (UTC)", "Open warnings");
        row = 2;
        foreach (var item in deliveries)
        {
            WriteRow(deliverySheet, row++,
                item.ExpectedDeliveryId.ToString(),
                item.MonitoringDefinitionId.ToString(),
                item.DefinitionName,
                item.ExpectedDate.ToString("yyyy-MM-dd"),
                item.Status,
                item.ExpectedFileCount.ToString(),
                item.ReceivedFileCount.ToString(),
                item.UnexpectedFileCount.ToString(),
                item.DeadlineUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                item.CompletedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty,
                item.WarningCount.ToString());
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// Text that starts with a formula character would run as a formula when the file is opened. A leading apostrophe stops that.
    public static string Safe(string value) =>
        FormulaStarts.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal)) ? "'" + value : value;

    private static void WriteHeader(IXLWorksheet sheet, params string[] headers)
    {
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
    }

    private static void WriteRow(IXLWorksheet sheet, int row, params string[] values)
    {
        for (var column = 0; column < values.Length; column++)
            sheet.Cell(row, column + 1).Value = Safe(values[column]);
    }
}
