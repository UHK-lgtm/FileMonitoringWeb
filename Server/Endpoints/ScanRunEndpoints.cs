using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.Domain;

namespace FileMonitoringWeb.Server.Endpoints;

/// Scan status endpoints (docs/api-contract.md, "Scan status"). Read-only.
public static class ScanRunEndpoints
{
    public static void MapScanRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/scan-runs/latest", async (ScanRunRepository repository, ILogger<ScanRunRepository> logger, CancellationToken cancellationToken) =>
        {
            try
            {
                var latest = await repository.LatestAsync(cancellationToken);
                var record = latest is null ? null : new ScanRunRecord(latest.Status, latest.StartedUtc, latest.FinishedUtc);
                var state = ScanStatusRules.Evaluate(record, DateTime.UtcNow);

                return Results.Ok(new { state = state.ToString(), latest });
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Log the exception type only: SQL messages can contain server names.
                logger.LogError("Reading the latest scan run failed: {ExceptionType}", exception.GetType().Name);
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Scan status is unavailable.");
            }
        });
    }
}
