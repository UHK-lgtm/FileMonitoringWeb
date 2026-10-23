using System.Data;
using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;
using Microsoft.Data.SqlClient;

namespace FileMonitoringWeb.Server.Data;

public enum SaveOutcome
{
    Saved,
    NotFound,
    Conflict,
    Invalid,
}

/// Reads and writes FileScan.MonitoringDefinition with parameterized SQL. Every change is written to the audit table in the
/// same transaction. Never logs or returns the connection string.
public sealed class MonitoringDefinitionRepository(IConfiguration configuration)
{
    private const string ConnectionName = "DefaultConnection";

    private const string Columns = """
        Id, Name, Provider, RelativePath, FileNamePattern, ExpectedFileCount, Frequency, DayOfWeek, IntervalDays,
        AnchorDate, DayOfMonth, MonthOfQuarter, ExpectedDeliveryTime, BufferMinutes, TimeZoneId, FilenameDateMode,
        FilenameDateFormat, SizeCheckEnabled, ReferenceSizeBytes, SizeTolerancePercent, IsEnabled, IsRetired, ConcurrencyToken
        """;

    private const string SelectAll = "SELECT " + Columns + " FROM FileScan.MonitoringDefinition ORDER BY Id;";
    private const string SelectOneForUpdate = "SELECT " + Columns + " FROM FileScan.MonitoringDefinition WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id;";
    private const string SelectOne = "SELECT " + Columns + " FROM FileScan.MonitoringDefinition WHERE Id = @Id;";

    private const string Insert = """
        INSERT INTO FileScan.MonitoringDefinition
            (Name, Provider, RelativePath, FileNamePattern, ExpectedFileCount, Frequency, DayOfWeek, IntervalDays,
             AnchorDate, DayOfMonth, MonthOfQuarter, ExpectedDeliveryTime, BufferMinutes, TimeZoneId, FilenameDateMode,
             FilenameDateFormat, SizeCheckEnabled, ReferenceSizeBytes, SizeTolerancePercent, IsEnabled)
        OUTPUT INSERTED.Id
        VALUES
            (@Name, @Provider, @RelativePath, @FileNamePattern, @ExpectedFileCount, @Frequency, @DayOfWeek, @IntervalDays,
             @AnchorDate, @DayOfMonth, @MonthOfQuarter, @ExpectedDeliveryTime, @BufferMinutes, @TimeZoneId, @FilenameDateMode,
             @FilenameDateFormat, @SizeCheckEnabled, @ReferenceSizeBytes, @SizeTolerancePercent, @IsEnabled);
        """;

    private const string Update = """
        UPDATE FileScan.MonitoringDefinition
        SET Name = @Name, Provider = @Provider, RelativePath = @RelativePath, FileNamePattern = @FileNamePattern,
            ExpectedFileCount = @ExpectedFileCount, Frequency = @Frequency, DayOfWeek = @DayOfWeek,
            IntervalDays = @IntervalDays, AnchorDate = @AnchorDate, DayOfMonth = @DayOfMonth,
            MonthOfQuarter = @MonthOfQuarter, ExpectedDeliveryTime = @ExpectedDeliveryTime, BufferMinutes = @BufferMinutes,
            TimeZoneId = @TimeZoneId, FilenameDateMode = @FilenameDateMode, FilenameDateFormat = @FilenameDateFormat,
            SizeCheckEnabled = @SizeCheckEnabled, ReferenceSizeBytes = @ReferenceSizeBytes,
            SizeTolerancePercent = @SizeTolerancePercent, IsEnabled = @IsEnabled, UpdatedUtc = SYSUTCDATETIME()
        WHERE Id = @Id AND ConcurrencyToken = @ConcurrencyToken;
        """;

    private const string Retire = """
        UPDATE FileScan.MonitoringDefinition
        SET IsRetired = 1, IsEnabled = 0, RetiredAtUtc = SYSUTCDATETIME(), RetiredBy = @RetiredBy, UpdatedUtc = SYSUTCDATETIME()
        WHERE Id = @Id;
        """;

    private const string SetEnabled = """
        UPDATE FileScan.MonitoringDefinition
        SET IsEnabled = @IsEnabled, UpdatedUtc = SYSUTCDATETIME()
        WHERE Id = @Id;
        """;

    private const string InsertAudit = """
        INSERT INTO FileScan.MonitoringDefinitionAudit (MonitoringDefinitionId, ChangedBy, FieldName, OldValue, NewValue)
        VALUES (@MonitoringDefinitionId, @ChangedBy, @FieldName, @OldValue, @NewValue);
        """;

    private const string SelectAudit = """
        SELECT AuditId, ChangedAtUtc, ChangedBy, FieldName, OldValue, NewValue
        FROM FileScan.MonitoringDefinitionAudit
        WHERE MonitoringDefinitionId = @Id
        ORDER BY ChangedAtUtc DESC, AuditId DESC;
        """;

    private const string SelectAllAudit = """
        SELECT a.AuditId, a.MonitoringDefinitionId, d.Name, a.ChangedAtUtc, a.ChangedBy, a.FieldName, a.OldValue, a.NewValue
        FROM FileScan.MonitoringDefinitionAudit AS a
        JOIN FileScan.MonitoringDefinition AS d ON d.Id = a.MonitoringDefinitionId
        ORDER BY a.ChangedAtUtc, a.AuditId;
        """;

    public async Task<IReadOnlyList<MonitoringDefinitionDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectAll, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var definitions = new List<MonitoringDefinitionDto>();
        while (await reader.ReadAsync(cancellationToken))
            definitions.Add(Read(reader));

        return definitions;
    }

    public async Task<MonitoringDefinitionDto?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectOne, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<int> CreateAsync(DefinitionInput input, string user, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction();

        await using var insert = new SqlCommand(Insert, connection, transaction);
        AddParameters(insert, input);
        var id = Convert.ToInt32(await insert.ExecuteScalarAsync(cancellationToken));

        await InsertAuditAsync(connection, transaction, id, user, [new AuditChange("(created)", null, input.Name?.Trim())], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return id;
    }

    /// Saves the full definition if the token still matches. Writes one audit row per changed field.
    public async Task<SaveOutcome> UpdateAsync(int id, DefinitionInput input, string user, CancellationToken cancellationToken = default)
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

        var updated = MonitoringDefinitionDto.FromInput(id, input, current.IsRetired, current.ConcurrencyToken);
        var changes = AuditDiff.Diff(current, updated);

        await using var command = new SqlCommand(Update, connection, transaction);
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

        await using var command = new SqlCommand(SetEnabled, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = isEnabled;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await InsertAuditAsync(connection, transaction, id, user, [new AuditChange("isEnabled", Flag(current.IsEnabled), Flag(isEnabled))], cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaveOutcome.Saved;
    }

    /// Soft delete. The row is kept with its history (spec section 21). It is never hard-deleted.
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

        await using var command = new SqlCommand(Retire, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        command.Parameters.Add("@RetiredBy", SqlDbType.NVarChar, 256).Value = user;
        await command.ExecuteNonQueryAsync(cancellationToken);

        var retired = current with { IsRetired = true, IsEnabled = false };
        await InsertAuditAsync(connection, transaction, id, user, AuditDiff.Diff(current, retired), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SaveOutcome.Saved;
    }

    public async Task<IReadOnlyList<AuditEntryDto>> AuditForAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectAudit, connection);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var entries = new List<AuditEntryDto>();
        while (await reader.ReadAsync(cancellationToken))
            entries.Add(ReadAudit(reader));

        return entries;
    }

    /// Every audit row with its definition name, for the Excel export.
    public async Task<IReadOnlyList<(int DefinitionId, string DefinitionName, AuditEntryDto Entry)>> AllAuditAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = new SqlCommand(SelectAllAudit, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<(int, string, AuditEntryDto)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            // Columns: AuditId, MonitoringDefinitionId, Name, ChangedAtUtc, ChangedBy, FieldName, OldValue, NewValue.
            var entry = new AuditEntryDto(
                reader.GetInt64(0),
                reader.GetDateTime(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7));
            rows.Add((reader.GetInt32(1), reader.GetString(2), entry));
        }

        return rows;
    }

    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString(ConnectionName)
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");

        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<MonitoringDefinitionDto?> ReadForUpdateAsync(
        SqlConnection connection, SqlTransaction transaction, int id, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(SelectOneForUpdate, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    private static async Task InsertAuditAsync(
        SqlConnection connection, SqlTransaction transaction, int id, string user,
        IEnumerable<AuditChange> changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            await using var command = new SqlCommand(InsertAudit, connection, transaction);
            command.Parameters.Add("@MonitoringDefinitionId", SqlDbType.Int).Value = id;
            command.Parameters.Add("@ChangedBy", SqlDbType.NVarChar, 256).Value = user;
            command.Parameters.Add("@FieldName", SqlDbType.NVarChar, 100).Value = change.FieldName;
            command.Parameters.Add("@OldValue", SqlDbType.NVarChar, -1).Value = (object?)change.OldValue ?? DBNull.Value;
            command.Parameters.Add("@NewValue", SqlDbType.NVarChar, -1).Value = (object?)change.NewValue ?? DBNull.Value;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static void AddParameters(SqlCommand command, DefinitionInput input)
    {
        var time = TimeOnly.ParseExact(input.ExpectedDeliveryTime!, "HH:mm");

        command.Parameters.Add("@Name", SqlDbType.NVarChar, 200).Value = input.Name!.Trim();
        command.Parameters.Add("@Provider", SqlDbType.NVarChar, 100).Value = (input.Provider ?? string.Empty).Trim();
        command.Parameters.Add("@RelativePath", SqlDbType.NVarChar, 400).Value = (input.RelativePath ?? string.Empty).Trim();
        command.Parameters.Add("@FileNamePattern", SqlDbType.NVarChar, 260).Value = input.FileNamePattern!.Trim();
        command.Parameters.Add("@ExpectedFileCount", SqlDbType.Int).Value = input.ExpectedFileCount;
        command.Parameters.Add("@Frequency", SqlDbType.NVarChar, 20).Value = input.Frequency!;
        command.Parameters.Add("@DayOfWeek", SqlDbType.TinyInt).Value = Nullable(input.IsoWeekday);
        command.Parameters.Add("@IntervalDays", SqlDbType.Int).Value = Nullable(input.IntervalDays);
        command.Parameters.Add("@AnchorDate", SqlDbType.Date).Value = input.AnchorDate is null ? DBNull.Value : input.AnchorDate.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@DayOfMonth", SqlDbType.TinyInt).Value = Nullable(input.DayOfMonth);
        command.Parameters.Add("@MonthOfQuarter", SqlDbType.TinyInt).Value = Nullable(input.MonthOfQuarter);
        command.Parameters.Add("@ExpectedDeliveryTime", SqlDbType.Time).Value = time.ToTimeSpan();
        command.Parameters.Add("@BufferMinutes", SqlDbType.Int).Value = input.BufferMinutes;
        command.Parameters.Add("@TimeZoneId", SqlDbType.NVarChar, 64).Value = input.TimeZoneId!.Trim();
        command.Parameters.Add("@FilenameDateMode", SqlDbType.NVarChar, 20).Value = input.FilenameDateMode!;
        command.Parameters.Add("@FilenameDateFormat", SqlDbType.NVarChar, 30).Value = (object?)input.FilenameDateFormat?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@SizeCheckEnabled", SqlDbType.Bit).Value = input.SizeCheckEnabled;
        command.Parameters.Add("@ReferenceSizeBytes", SqlDbType.BigInt).Value = Nullable(input.ReferenceSizeBytes);
        command.Parameters.Add("@SizeTolerancePercent", SqlDbType.Decimal).Value = Nullable(input.SizeTolerancePercent);
        command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = input.IsEnabled;
    }

    private static MonitoringDefinitionDto Read(SqlDataReader reader) =>
        new(
            Id: reader.GetInt32(0),
            Name: reader.GetString(1),
            Provider: reader.GetString(2),
            RelativePath: reader.GetString(3),
            FileNamePattern: reader.GetString(4),
            ExpectedFileCount: reader.GetInt32(5),
            Frequency: reader.GetString(6),
            IsoWeekday: NullableInt(reader, 7),
            IntervalDays: NullableInt(reader, 8),
            AnchorDate: reader.IsDBNull(9) ? null : reader.GetFieldValue<DateOnly>(9),
            DayOfMonth: NullableInt(reader, 10),
            MonthOfQuarter: NullableInt(reader, 11),
            ExpectedDeliveryTime: reader.GetFieldValue<TimeOnly>(12).ToString("HH:mm"),
            BufferMinutes: reader.GetInt32(13),
            TimeZoneId: reader.GetString(14),
            FilenameDateMode: reader.GetString(15),
            FilenameDateFormat: reader.IsDBNull(16) ? null : reader.GetString(16),
            SizeCheckEnabled: reader.GetBoolean(17),
            ReferenceSizeBytes: reader.IsDBNull(18) ? null : reader.GetInt64(18),
            SizeTolerancePercent: reader.IsDBNull(19) ? null : reader.GetDecimal(19),
            IsEnabled: reader.GetBoolean(20),
            IsRetired: reader.GetBoolean(21),
            ConcurrencyToken: Convert.ToBase64String(reader.GetFieldValue<byte[]>(22)));

    /// Columns: AuditId, ChangedAtUtc, ChangedBy, FieldName, OldValue, NewValue.
    private static AuditEntryDto ReadAudit(SqlDataReader reader) =>
        new(
            AuditId: reader.GetInt64(0),
            ChangedAtUtc: reader.GetDateTime(1),
            ChangedBy: reader.GetString(2),
            FieldName: reader.GetString(3),
            OldValue: reader.IsDBNull(4) ? null : reader.GetString(4),
            NewValue: reader.IsDBNull(5) ? null : reader.GetString(5));

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

    private static string Flag(bool value) => value ? "true" : "false";

    private static object Nullable<T>(T? value) where T : struct => value.HasValue ? value.Value : DBNull.Value;

    private static int? NullableInt(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
}
