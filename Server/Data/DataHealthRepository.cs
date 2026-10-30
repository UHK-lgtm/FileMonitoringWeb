using System.Data;
using FileMonitoringWeb.Server.DataHealth;
using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;
using Microsoft.Data.SqlClient;

namespace FileMonitoringWeb.Server.Data;

/// One enabled control, joined to its approved source, ready to run.
public sealed record RunnableControl(
    int ControlId,
    string Name,
    int OffsetDays,
    string OffsetType,
    int HistoryDays,
    bool MissingCheck,
    bool DecreaseCheck,
    decimal Tolerance,
    string SourceName,
    string SchemaName,
    string ObjectName,
    string DateColumn,
    string DateColumnType,
    string? DateStyles);

/// The rows a count returned, and how many text values matched none of the styles.
public sealed record CountResult(IReadOnlyDictionary<DateOnly, int> CountsByDay, int UnreadableRows);

/// Data Health persistence. Parameterized SQL for every value. Table and column names come only from the approved
/// source list, are checked against a strict pattern, and are checked against the catalog before they are used.
/// Never logs or returns the connection string.
public sealed class DataHealthRepository(IConfiguration configuration)
{
    private const string ConnectionName = "DefaultConnection";
    private const int CommandTimeoutSeconds = 15;

    private const string ControlColumns = """
        c.ControlId, c.Name, c.DisplayOrder, c.IsEnabled, c.ApprovedSourceId, s.SourceKey, s.DisplayName,
        c.OffsetDays, c.OffsetType, c.HistoryDays, c.MissingCheck, c.DecreaseCheck, c.DecreaseTolerancePercent,
        c.DateColumnType, c.DateStyles, c.IsRetired, c.ConcurrencyToken
        """;

    private const string SelectControls = "SELECT " + ControlColumns + """

        FROM FileScan.DataHealthControl AS c
        JOIN FileScan.DataHealthSource AS s ON s.ApprovedSourceId = c.ApprovedSourceId
        ORDER BY c.DisplayOrder, c.ControlId;
        """;

    private const string SelectControl = "SELECT " + ControlColumns + """

        FROM FileScan.DataHealthControl AS c
        JOIN FileScan.DataHealthSource AS s ON s.ApprovedSourceId = c.ApprovedSourceId
        WHERE c.ControlId = @Id;
        """;

    private const string SelectControlForUpdate = "SELECT " + ControlColumns + """

        FROM FileScan.DataHealthControl AS c WITH (UPDLOCK, HOLDLOCK)
        JOIN FileScan.DataHealthSource AS s ON s.ApprovedSourceId = c.ApprovedSourceId
        WHERE c.ControlId = @Id;
        """;

    private const string RunnableColumns = """
        SELECT c.ControlId, c.Name, c.OffsetDays, c.OffsetType, c.HistoryDays, c.MissingCheck, c.DecreaseCheck,
               c.DecreaseTolerancePercent, s.DisplayName, s.SchemaName, s.ObjectName, s.DateColumn,
               c.DateColumnType, c.DateStyles
        FROM FileScan.DataHealthControl AS c
        JOIN FileScan.DataHealthSource AS s ON s.ApprovedSourceId = c.ApprovedSourceId
        """;

    private const string SelectRunnable = RunnableColumns + """

        WHERE c.IsEnabled = 1 AND c.IsRetired = 0 AND s.IsActive = 1
        ORDER BY c.DisplayOrder, c.ControlId;
        """;

    private const string SelectRunnableById = RunnableColumns + """

        WHERE c.ControlId = @Id AND c.IsEnabled = 1 AND c.IsRetired = 0 AND s.IsActive = 1;
        """;

    private const string SelectSources = """
        SELECT ApprovedSourceId, SourceKey, DisplayName, SchemaName, ObjectName, DateColumn
        FROM FileScan.DataHealthSource
        WHERE IsActive = 1
        ORDER BY DisplayName;
        """;

    private const string SelectSource = """
        SELECT ApprovedSourceId, SourceKey, DisplayName, SchemaName, ObjectName, DateColumn
        FROM FileScan.DataHealthSource
        WHERE ApprovedSourceId = @Id AND IsActive = 1;
        """;

    // The column must exist on the named table or view, with the type the control says. Values are parameters.
    private const string SelectColumnType = """
        SELECT typ.name
        FROM sys.columns AS col
        JOIN sys.objects AS obj ON obj.object_id = col.object_id
        JOIN sys.schemas AS sch ON sch.schema_id = obj.schema_id
        JOIN sys.types AS typ ON typ.user_type_id = col.user_type_id
        WHERE sch.name = @Schema AND obj.name = @Object AND col.name = @Column;
        """;

    private const string InsertControl = """
        INSERT INTO FileScan.DataHealthControl
            (Name, DisplayOrder, IsEnabled, ApprovedSourceId, OffsetDays, OffsetType, HistoryDays, MissingCheck,
             DecreaseCheck, DecreaseTolerancePercent, DateColumnType, DateStyles)
        OUTPUT INSERTED.ControlId
        VALUES (@Name, @DisplayOrder, @IsEnabled, @ApprovedSourceId, @OffsetDays, @OffsetType, @HistoryDays,
                @MissingCheck, @DecreaseCheck, @DecreaseTolerancePercent, @DateColumnType, @DateStyles);
        """;

    private const string UpdateControl = """
        UPDATE FileScan.DataHealthControl
        SET Name = @Name, DisplayOrder = @DisplayOrder, IsEnabled = @IsEnabled, ApprovedSourceId = @ApprovedSourceId,
            OffsetDays = @OffsetDays, OffsetType = @OffsetType, HistoryDays = @HistoryDays,
            MissingCheck = @MissingCheck, DecreaseCheck = @DecreaseCheck,
            DecreaseTolerancePercent = @DecreaseTolerancePercent, DateColumnType = @DateColumnType,
            DateStyles = @DateStyles, UpdatedUtc = SYSUTCDATETIME()
        WHERE ControlId = @Id AND ConcurrencyToken = @ConcurrencyToken;
        """;

    private const string SetEnabled = """
        UPDATE FileScan.DataHealthControl SET IsEnabled = @IsEnabled, UpdatedUtc = SYSUTCDATETIME() WHERE ControlId = @Id;
        """;

    private const string RetireControl = """
        UPDATE FileScan.DataHealthControl
        SET IsRetired = 1, IsEnabled = 0, RetiredAtUtc = SYSUTCDATETIME(), RetiredBy = @RetiredBy, UpdatedUtc = SYSUTCDATETIME()
        WHERE ControlId = @Id;
        """;

    private const string InsertAudit = """
        INSERT INTO FileScan.DataHealthControlAudit (ControlId, ChangedBy, FieldName, OldValue, NewValue)
        VALUES (@ControlId, @ChangedBy, @FieldName, @OldValue, @NewValue);
        """;

    private const string SelectAudit = """
        SELECT AuditId, ChangedAtUtc, ChangedBy, FieldName, OldValue, NewValue
        FROM FileScan.DataHealthControlAudit
        WHERE ControlId = @Id
        ORDER BY ChangedAtUtc DESC, AuditId DESC;
        """;

    public async Task<IReadOnlyList<ApprovedSourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectSources, connection) { CommandTimeout = CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var sources = new List<ApprovedSourceDto>();
        while (await reader.ReadAsync(cancellationToken))
            sources.Add(ReadSource(reader));

        return sources;
    }

    public async Task<ApprovedSourceDto?> GetSourceAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectSource, connection) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadSource(reader) : null;
    }

    public async Task<IReadOnlyList<DataHealthControlDto>> ListControlsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectControls, connection) { CommandTimeout = CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var controls = new List<DataHealthControlDto>();
        while (await reader.ReadAsync(cancellationToken))
            controls.Add(ReadControl(reader));

        return controls;
    }

    public async Task<DataHealthControlDto?> GetControlAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectControl, connection) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadControl(reader) : null;
    }

    public async Task<IReadOnlyList<RunnableControl>> ListRunnableAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectRunnable, connection) { CommandTimeout = CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var controls = new List<RunnableControl>();
        while (await reader.ReadAsync(cancellationToken))
            controls.Add(ReadRunnable(reader));

        return controls;
    }

    /// One enabled control, or null when it is missing, retired, disabled, or its source is inactive.
    public async Task<RunnableControl?> GetRunnableAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectRunnableById, connection) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadRunnable(reader) : null;
    }

    private static RunnableControl ReadRunnable(SqlDataReader reader) =>
        new(
            ControlId: reader.GetInt32(0),
            Name: reader.GetString(1),
            OffsetDays: reader.GetInt32(2),
            OffsetType: reader.GetString(3),
            HistoryDays: reader.GetInt32(4),
            MissingCheck: reader.GetBoolean(5),
            DecreaseCheck: reader.GetBoolean(6),
            Tolerance: reader.GetDecimal(7),
            SourceName: reader.GetString(8),
            SchemaName: reader.GetString(9),
            ObjectName: reader.GetString(10),
            DateColumn: reader.GetString(11),
            DateColumnType: reader.GetString(12),
            DateStyles: reader.IsDBNull(13) ? null : reader.GetString(13));

    /// Counts records per day in [from, toExclusive). Names must be valid and must exist in the catalog with the configured type.
    /// Text columns are read with the configured styles; values that match none of them are counted as unreadable.
    public async Task<CountResult> CountByDayAsync(
        RunnableControl control, DateOnly from, DateOnly toExclusive, CancellationToken cancellationToken = default)
    {
        if (!DataHealthRules.IsValidName(control.SchemaName)
            || !DataHealthRules.IsValidName(control.ObjectName)
            || !DataHealthRules.IsValidName(control.DateColumn))
            throw new InvalidOperationException("The approved source has an invalid name.");

        var isText = DataHealthRules.IsTextType(control.DateColumnType);
        var styles = isText ? DataHealthRules.ParseStyles(control.DateStyles) : null;
        if (isText && styles is null)
            throw new InvalidOperationException("The control has no valid date styles.");

        await using var connection = await OpenAsync(cancellationToken);

        string? actualType;
        await using (var check = new SqlCommand(SelectColumnType, connection) { CommandTimeout = CommandTimeoutSeconds })
        {
            check.Parameters.Add("@Schema", SqlDbType.NVarChar, 128).Value = control.SchemaName;
            check.Parameters.Add("@Object", SqlDbType.NVarChar, 128).Value = control.ObjectName;
            check.Parameters.Add("@Column", SqlDbType.NVarChar, 128).Value = control.DateColumn;
            actualType = await check.ExecuteScalarAsync(cancellationToken) as string;
        }

        var typeMatches = isText
            ? DataHealthRules.IsTextType(actualType)
            : string.Equals(actualType, control.DateColumnType, StringComparison.OrdinalIgnoreCase);
        if (actualType is null || !typeMatches)
            throw new InvalidOperationException("The control's column type does not match the source column.");

        // Only names that passed the validation and the catalog check are quoted into the text. Values are parameters.
        var sql = isText
            ? DataHealthSql.CountText(control.SchemaName, control.ObjectName, control.DateColumn, styles!)
            : DataHealthSql.CountDates(control.SchemaName, control.ObjectName, control.DateColumn);

        var counts = new Dictionary<DateOnly, int>();
        await using (var command = new SqlCommand(sql, connection) { CommandTimeout = CommandTimeoutSeconds })
        {
            command.Parameters.Add("@From", SqlDbType.Date).Value = from.ToDateTime(TimeOnly.MinValue);
            command.Parameters.Add("@ToExclusive", SqlDbType.Date).Value = toExclusive.ToDateTime(TimeOnly.MinValue);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                    counts[reader.GetFieldValue<DateOnly>(0)] = reader.GetInt32(1);
            }
        }

        var unreadable = 0;
        if (isText)
        {
            await using var unreadableCommand = new SqlCommand(DataHealthSql.UnreadableText(control.SchemaName, control.ObjectName, control.DateColumn, styles!), connection)
            {
                CommandTimeout = CommandTimeoutSeconds,
            };
            unreadable = Convert.ToInt32(await unreadableCommand.ExecuteScalarAsync(cancellationToken));
        }

        return new CountResult(counts, unreadable);
    }

    public async Task<int> CreateControlAsync(DataHealthControlInput input, string user, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using var insert = new SqlCommand(InsertControl, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
        AddParameters(insert, input);
        var id = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));

        await InsertAuditAsync(connection, transaction, id, user, [new AuditChange("(created)", null, input.Name?.Trim())], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    /// Saves the control if its token still matches. Writes one audit row per changed field.
    public async Task<SaveOutcome> UpdateControlAsync(int id, DataHealthControlInput input, string user, CancellationToken cancellationToken = default)
    {
        var token = input.ConcurrencyToken is null ? null : TryDecodeToken(input.ConcurrencyToken);
        if (token is null)
            return SaveOutcome.Conflict;

        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var current = await ReadForUpdateAsync(connection, transaction, id, cancellationToken);
        if (current is null)
            return SaveOutcome.NotFound;

        if (current.ConcurrencyToken != input.ConcurrencyToken)
            return SaveOutcome.Conflict;

        var updated = current with
        {
            Name = input.Name!.Trim(),
            DisplayOrder = input.DisplayOrder,
            IsEnabled = input.IsEnabled,
            ApprovedSourceId = input.ApprovedSourceId,
            OffsetDays = input.OffsetDays,
            OffsetType = input.OffsetType!,
            HistoryDays = input.HistoryDays,
            MissingCheck = input.MissingCheck,
            DecreaseCheck = input.DecreaseCheck,
            DecreaseTolerancePercent = input.DecreaseTolerancePercent,
            DateColumnType = input.DateColumnType!,
            DateStyles = DataHealthRules.NormalizeStyles(input.DateColumnType, input.DateStyles),
        };
        var changes = ControlAuditDiff.Diff(current, updated);

        await using var command = new SqlCommand(UpdateControl, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
        AddParameters(command, input);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@ConcurrencyToken", SqlDbType.VarBinary, 8).Value = token;

        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            return SaveOutcome.Conflict;

        await InsertAuditAsync(connection, transaction, id, user, changes, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaveOutcome.Saved;
    }

    public async Task<SaveOutcome> SetEnabledAsync(int id, bool isEnabled, string user, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var current = await ReadForUpdateAsync(connection, transaction, id, cancellationToken);
        if (current is null)
            return SaveOutcome.NotFound;

        if (isEnabled && current.IsRetired)
            return SaveOutcome.Invalid;

        if (current.IsEnabled == isEnabled)
        {
            await transaction.CommitAsync(cancellationToken);
            return SaveOutcome.Saved;
        }

        await using var command = new SqlCommand(SetEnabled, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = isEnabled;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var after = current with { IsEnabled = isEnabled };
        await InsertAuditAsync(connection, transaction, id, user, ControlAuditDiff.Diff(current, after), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaveOutcome.Saved;
    }

    /// Soft delete. The control and its history are kept (spec section 9). It is never hard-deleted.
    public async Task<SaveOutcome> RetireAsync(int id, string user, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        var current = await ReadForUpdateAsync(connection, transaction, id, cancellationToken);
        if (current is null)
            return SaveOutcome.NotFound;

        if (current.IsRetired)
        {
            await transaction.CommitAsync(cancellationToken);
            return SaveOutcome.Saved;
        }

        await using var command = new SqlCommand(RetireControl, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@RetiredBy", SqlDbType.NVarChar, 256).Value = user;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var retired = current with { IsRetired = true, IsEnabled = false };
        await InsertAuditAsync(connection, transaction, id, user, ControlAuditDiff.Diff(current, retired), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaveOutcome.Saved;
    }

    public async Task<IReadOnlyList<AuditEntryDto>> AuditForAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectAudit, connection) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var entries = new List<AuditEntryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new AuditEntryDto(
                reader.GetInt64(0),
                reader.GetDateTime(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return entries;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<DataHealthControlDto?> ReadForUpdateAsync(
        SqlConnection connection, SqlTransaction transaction, int id, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectControlForUpdate, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? ReadControl(reader) : null;
    }

    private static async Task InsertAuditAsync(
        SqlConnection connection, SqlTransaction transaction, int id, string user,
        IEnumerable<AuditChange> changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            await using var command = new SqlCommand(InsertAudit, connection, transaction) { CommandTimeout = CommandTimeoutSeconds };
            command.Parameters.Add("@ControlId", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ChangedBy", SqlDbType.NVarChar, 256).Value = user;
            command.Parameters.Add("@FieldName", SqlDbType.NVarChar, 100).Value = change.FieldName;
            command.Parameters.Add("@OldValue", SqlDbType.NVarChar, -1).Value = (object?)change.OldValue ?? DBNull.Value;
            command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = (object?)change.NewValue ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddParameters(SqlCommand command, DataHealthControlInput input)
    {
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = input.Name!.Trim();
        command.Parameters.Add("@DisplayOrder", SqlDbType.Int).Value = input.DisplayOrder;
        command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = input.IsEnabled;
        command.Parameters.Add("@ApprovedSourceId", SqlDbType.Int).Value = input.ApprovedSourceId;
        command.Parameters.Add("@OffsetDays", SqlDbType.Int).Value = input.OffsetDays;
        command.Parameters.Add("@OffsetType", SqlDbType.NVarChar, 20).Value = input.OffsetType!;
        command.Parameters.Add("@HistoryDays", SqlDbType.Int).Value = input.HistoryDays;
        command.Parameters.Add("@MissingCheck", SqlDbType.Bit).Value = input.MissingCheck;
        command.Parameters.Add("@DecreaseCheck", SqlDbType.Bit).Value = input.DecreaseCheck;
        command.Parameters.Add("@DecreaseTolerancePercent", SqlDbType.Decimal).Value = input.DecreaseTolerancePercent;
        command.Parameters.Add("@DateColumnType", SqlDbType.NVarChar, 20).Value = input.DateColumnType!;
        command.Parameters.Add("@DateStyles", SqlDbType.NVarChar, 100).Value =
            (object?)DataHealthRules.NormalizeStyles(input.DateColumnType, input.DateStyles) ?? DBNull.Value;
    }

    private static ApprovedSourceDto ReadSource(SqlDataReader reader) =>
        new(
            Id: reader.GetInt32(0),
            SourceKey: reader.GetString(1),
            DisplayName: reader.GetString(2),
            SchemaName: reader.GetString(3),
            ObjectName: reader.GetString(4),
            DateColumn: reader.GetString(5));

    private static DataHealthControlDto ReadControl(SqlDataReader reader) =>
        new(
            Id: reader.GetInt32(0),
            Name: reader.GetString(1),
            DisplayOrder: reader.GetInt32(2),
            IsEnabled: reader.GetBoolean(3),
            ApprovedSourceId: reader.GetInt32(4),
            SourceKey: reader.GetString(5),
            SourceName: reader.GetString(6),
            OffsetDays: reader.GetInt32(7),
            OffsetType: reader.GetString(8),
            HistoryDays: reader.GetInt32(9),
            MissingCheck: reader.GetBoolean(10),
            DecreaseCheck: reader.GetBoolean(11),
            DecreaseTolerancePercent: reader.GetDecimal(12),
            DateColumnType: reader.GetString(13),
            DateStyles: reader.IsDBNull(14) ? null : reader.GetString(14),
            IsRetired: reader.GetBoolean(15),
            ConcurrencyToken: Convert.ToBase64String(reader.GetFieldValue<byte[]>(16)));

    private static byte[]? TryDecodeToken(string token)
    {
        try
        {
            var bytes = Convert.FromBase64String(token);
            return bytes.Length == 8 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
