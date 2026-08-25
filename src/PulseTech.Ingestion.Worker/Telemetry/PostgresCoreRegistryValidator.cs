using Dapper;
using Npgsql;

namespace PulseTech.Ingestion.Worker.Telemetry;

public sealed class PostgresCoreRegistryValidator : ICoreRegistryValidator
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresCoreRegistryValidator(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<string?> ResolveApplicationCodeAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT app_code FROM core.applications WHERE id = @ApplicationId";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(sql, new { ApplicationId = applicationId }, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(command);
    }

    public async Task<bool> DeviceBelongsToApplicationAsync(Guid deviceId, Guid applicationId, CancellationToken cancellationToken)
    {
        // core.devices.id is char(36); comparing as text keeps this agnostic to the exact uuid/char column type.
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM core.devices
                WHERE id::text = @DeviceId AND app_id = @ApplicationId AND is_active
            )
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            sql,
            new { DeviceId = deviceId.ToString(), ApplicationId = applicationId },
            cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(command);
    }
}
