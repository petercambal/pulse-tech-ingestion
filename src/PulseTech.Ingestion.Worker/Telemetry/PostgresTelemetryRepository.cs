using System.Text.Json;
using Dapper;
using Npgsql;

namespace PulseTech.Ingestion.Worker.Telemetry;

/// <summary>
/// Inserts telemetry into the per-application table. Every application-owned telemetry table is
/// expected to expose (device_id, time) plus either:
///  - explicit typed columns matching payload JSON keys 1:1 (<see cref="ApplicationSchemaMapping.Columns"/>),
///    e.g. smart-pot's soil_moisture/light_lux/air_temp_c, or
///  - a single jsonb column holding the raw payload (<see cref="ApplicationSchemaMapping.PayloadColumn"/>).
///
/// Schema/table/column names are never taken from the MQTT payload or topic directly; they come from
/// <see cref="ApplicationSchemaMapping"/>, which is validated against an identifier allowlist at
/// startup (see <see cref="ApplicationSchemaOptions.Validate"/>), so string-building the SQL here is safe.
/// </summary>
public sealed class PostgresTelemetryRepository : ITelemetryRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresTelemetryRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InsertAsync(ApplicationSchemaMapping mapping, TelemetryMessage message, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("device_id", message.DeviceId);
        parameters.Add("time", message.ReceivedAt);

        var columnNames = new List<string> { "device_id", "time" };

        if (mapping.Columns is { Length: > 0 })
        {
            using var payload = JsonDocument.Parse(message.RawPayload);
            foreach (var column in mapping.Columns)
            {
                columnNames.Add(column);
                parameters.Add(column, ExtractValue(payload.RootElement, column));
            }
        }
        else
        {
            columnNames.Add(mapping.PayloadColumn!);
            parameters.Add(mapping.PayloadColumn!, message.RawPayload);
        }

        var columnList = string.Join(", ", columnNames.Select(c => $"\"{c}\""));
        var valueList = mapping.Columns is { Length: > 0 }
            ? string.Join(", ", columnNames.Select(c => $"@{c}"))
            : string.Join(", ", columnNames.Take(2).Select(c => $"@{c}").Append($"@{mapping.PayloadColumn}::jsonb"));

        var sql = $"""INSERT INTO "{mapping.Schema}"."{mapping.Table}" ({columnList}) VALUES ({valueList})""";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    private static object? ExtractValue(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True or JsonValueKind.False => value.GetBoolean(),
            _ => value.GetRawText()
        };
    }
}
