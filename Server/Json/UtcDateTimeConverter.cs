using System.Text.Json;
using System.Text.Json.Serialization;

namespace FileMonitoringWeb.Server.Json;

/// Writes every DateTime as UTC with a trailing Z. The database stores UTC, but the values come back with no kind,
/// and the browser reads a value with no Z as its own local time. Without the Z, a deadline shows an hour early in Luxembourg.
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);

        writer.WriteStringValue(utc);
    }
}
