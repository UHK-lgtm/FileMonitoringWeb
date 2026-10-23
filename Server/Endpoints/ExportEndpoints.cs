using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Export;

namespace FileMonitoringWeb.Server.Endpoints;

/// Excel export of the configuration audit and the deliveries. Built from saved SQL data; the file share is not scanned.
public static class ExportEndpoints
{
    private const string SpreadsheetContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static void MapExportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/export/audit.xlsx", async (MonitoringDefinitionRepository definitions, DeliveryRepository deliveries, CancellationToken cancellationToken) =>
        {
            var audit = await definitions.AllAuditAsync(cancellationToken);
            var items = await deliveries.ListAsync(null, cancellationToken);
            var bytes = AuditWorkbook.Build(audit, items);

            var fileName = $"file-monitoring-audit-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
            return Results.File(bytes, SpreadsheetContentType, fileName);
        });
    }
}
