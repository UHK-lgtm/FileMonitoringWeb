using FileMonitoringWeb.Server.Models;
using Microsoft.Data.SqlClient;

namespace FileMonitoringWeb.Server.Data;

/// Read-only access to FileScan.MonitoringDefinition. Never logs or returns the connection string.
public sealed class MonitoringDefinitionRepository(IConfiguration configuration)
{
    private const string SelectAll = """
        SELECT Id, Name, RelativePath, FileNamePattern, IsEnabled
        FROM FileScan.MonitoringDefinition
        ORDER BY Id;
        """;

    public async Task<IReadOnlyList<MonitoringDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(SelectAll, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var definitions = new List<MonitoringDefinition>();
        while (await reader.ReadAsync(cancellationToken))
        {
            definitions.Add(new MonitoringDefinition(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4)));
        }

        return definitions;
    }
}
