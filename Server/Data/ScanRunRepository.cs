using FileMonitoringWeb.Server.Models;
using Microsoft.Data.SqlClient;

namespace FileMonitoringWeb.Server.Data;

/// Reads the latest FileScan.ScanRun row. Parameterized SQL only. Never logs or returns the connection string.
/// Rows are written by FileScan, not here.
public sealed class ScanRunRepository(IConfiguration configuration)
{
    private const string ConnectionName = "DefaultConnection";

    private const string SelectLatest = """
        SELECT TOP (1) ScanRunId, StartedUtc, FinishedUtc, Status, ScheduledToday, OpenDeliveries, StatusChanges
        FROM FileScan.ScanRun
        ORDER BY StartedUtc DESC, ScanRunId DESC;
        """;

    /// Null when no run has been recorded yet.
    public async Task<LatestScanRunDto?> LatestAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectLatest, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new LatestScanRunDto(
            reader.GetInt64(0),
            reader.GetDateTime(1),
            reader.IsDBNull(2) ? null : reader.GetDateTime(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            reader.IsDBNull(5) ? null : reader.GetInt32(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6));
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName) ?? configuration[ConnectionName];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DefaultConnection is not set.");

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
