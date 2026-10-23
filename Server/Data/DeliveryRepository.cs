using System.Data;
using FileMonitoringWeb.Server.Models;
using Microsoft.Data.SqlClient;

namespace FileMonitoringWeb.Server.Data;

/// Reads expected deliveries, their evidence and comments, and adds comments. Parameterized SQL only.
/// Never logs or returns the connection string. Expected deliveries are written by FileScan, not here.
public sealed class DeliveryRepository(IConfiguration configuration)
{
    private const string ConnectionName = "DefaultConnection";

    private const string SelectList = """
        SELECT d.ExpectedDeliveryId, d.MonitoringDefinitionId, m.Name, d.ExpectedDate, d.CurrentStatus,
               d.ExpectedFileCount, d.ReceivedFileCount,
               (SELECT COUNT(DISTINCT o.FileName) FROM FileScan.FileObservation AS o
                 WHERE o.ExpectedDeliveryId = d.ExpectedDeliveryId AND o.IsUnexpected = 1) AS UnexpectedFileCount,
               d.DeadlineUtc, d.CompletedAtUtc,
               (SELECT COUNT(*) FROM FileScan.DeliveryWarning AS w
                 WHERE w.ExpectedDeliveryId = d.ExpectedDeliveryId AND w.ResolvedAtUtc IS NULL) AS WarningCount
        FROM FileScan.ExpectedDelivery AS d
        JOIN FileScan.MonitoringDefinition AS m ON m.Id = d.MonitoringDefinitionId
        WHERE (@Date IS NULL OR d.ExpectedDate = @Date)
        ORDER BY d.ExpectedDate DESC, m.Name;
        """;

    private const string SelectFacts = """
        SELECT d.ExpectedDeliveryId, d.MonitoringDefinitionId, m.Name, d.ExpectedDate, d.RelativePath, d.FileNamePattern,
               d.TimeZoneId, d.CurrentStatus, d.ExpectedFileCount, d.ReceivedFileCount,
               (SELECT COUNT(DISTINCT o.FileName) FROM FileScan.FileObservation AS o
                 WHERE o.ExpectedDeliveryId = d.ExpectedDeliveryId AND o.IsUnexpected = 1) AS UnexpectedFileCount,
               d.DeadlineUtc, d.CompletedAtUtc
        FROM FileScan.ExpectedDelivery AS d
        JOIN FileScan.MonitoringDefinition AS m ON m.Id = d.MonitoringDefinitionId
        WHERE d.ExpectedDeliveryId = @Id;
        """;

    private const string SelectTransitions = """
        SELECT FromStatus, ToStatus, OccurredAtUtc, RecordedAtUtc, ExpectedFileCount, ReceivedFileCount, DeadlineUtc
        FROM FileScan.DeliveryStatusTransition
        WHERE ExpectedDeliveryId = @Id
        ORDER BY OccurredAtUtc, TransitionId;
        """;

    private const string SelectFiles = """
        SELECT FileName, RelativePath, SizeBytes, FileCreatedUtc, FileLastModifiedUtc, ObservedAtUtc, IsUnexpected
        FROM FileScan.FileObservation
        WHERE ExpectedDeliveryId = @Id
        ORDER BY ObservedAtUtc, ObservationId;
        """;

    private const string SelectWarnings = """
        SELECT WarningType, FileName, Detail, DetectedAtUtc, ResolvedAtUtc
        FROM FileScan.DeliveryWarning
        WHERE ExpectedDeliveryId = @Id
        ORDER BY DetectedAtUtc, WarningId;
        """;

    private const string SelectComments = """
        SELECT CommentId, CommentText, CreatedBy, CreatedAtUtc
        FROM FileScan.IncidentComment
        WHERE ExpectedDeliveryId = @Id
        ORDER BY CreatedAtUtc, CommentId;
        """;

    private const string InsertComment = """
        INSERT INTO FileScan.IncidentComment (ExpectedDeliveryId, CommentText, CreatedBy)
        OUTPUT INSERTED.CommentId, INSERTED.CommentText, INSERTED.CreatedBy, INSERTED.CreatedAtUtc
        SELECT @Id, @Text, @CreatedBy
        WHERE EXISTS (SELECT 1 FROM FileScan.ExpectedDelivery WHERE ExpectedDeliveryId = @Id);
        """;

    public async Task<IReadOnlyList<DeliveryListItemDto>> ListAsync(DateOnly? date, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectList, connection);
        command.Parameters.Add("@Date", SqlDbType.Date).Value = date is null ? DBNull.Value : date.Value.ToDateTime(TimeOnly.MinValue);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var items = new List<DeliveryListItemDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new DeliveryListItemDto(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetFieldValue<DateOnly>(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetInt32(6),
                reader.GetInt32(7),
                reader.GetDateTime(8),
                reader.IsDBNull(9) ? null : reader.GetDateTime(9),
                reader.GetInt32(10)));
        }

        return items;
    }

    /// Null when the delivery does not exist.
    public async Task<DeliveryDetailDto?> GetDetailAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);

        DeliveryFactsDto? facts;
        await using (var command = new SqlCommand(SelectFacts, connection))
        {
            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            facts = new DeliveryFactsDto(
                reader.GetInt64(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetFieldValue<DateOnly>(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetDateTime(11),
                reader.IsDBNull(12) ? null : reader.GetDateTime(12));
        }

        var transitions = await ReadAllAsync(connection, SelectTransitions, id, r => new TransitionDto(
            r.IsDBNull(0) ? null : r.GetString(0),
            r.GetString(1),
            r.GetDateTime(2),
            r.GetDateTime(3),
            r.GetInt32(4),
            r.GetInt32(5),
            r.GetDateTime(6)), cancellationToken);

        var files = await ReadAllAsync(connection, SelectFiles, id, r => new FileObservationDto(
            r.GetString(0),
            r.GetString(1),
            r.GetInt64(2),
            r.IsDBNull(3) ? null : r.GetDateTime(3),
            r.IsDBNull(4) ? null : r.GetDateTime(4),
            r.GetDateTime(5),
            r.GetBoolean(6)), cancellationToken);

        var warnings = await ReadAllAsync(connection, SelectWarnings, id, r => new WarningDto(
            r.GetString(0),
            r.GetString(1),
            r.IsDBNull(2) ? null : r.GetString(2),
            r.GetDateTime(3),
            r.IsDBNull(4) ? null : r.GetDateTime(4)), cancellationToken);

        var comments = await ReadAllAsync(connection, SelectComments, id, r => new CommentDto(
            r.GetInt64(0),
            r.GetString(1),
            r.GetString(2),
            r.GetDateTime(3)), cancellationToken);

        return new DeliveryDetailDto(facts, transitions, files, warnings, comments);
    }

    /// Null when the delivery does not exist. Comments are append-only.
    public async Task<CommentDto?> AddCommentAsync(long id, string text, string user, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(InsertComment, connection);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@Text", SqlDbType.NVarChar, 2000).Value = text;
        command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 256).Value = user;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken)
            ? new CommentDto(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3))
            : null;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<IReadOnlyList<T>> ReadAllAsync<T>(
        SqlConnection connection, string sql, long id, Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(map(reader));

        return rows;
    }
}
