namespace PulseTech.Ingestion.Worker.Telemetry;

/// <summary>
/// Resolves and validates the application_id/device_id parsed from an inbound MQTT topic against
/// the core registry (core.applications / core.devices) before we attempt to insert telemetry.
///
/// The MQTT topic carries the actual primary keys — core.applications.id and core.devices.id (both
/// uuid) — not the human-readable core.applications.app_code. We still resolve app_code because the
/// TelemetryRouting configuration (which schema/table owns an application's telemetry) is keyed by
/// it rather than by raw uuid. core.devices.app_id is a FK to core.applications, so device validation
/// also confirms the device belongs to the resolved application rather than just existing somewhere
/// in the table.
/// </summary>
public interface ICoreRegistryValidator
{
    Task<string?> ResolveApplicationCodeAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<bool> DeviceBelongsToApplicationAsync(Guid deviceId, Guid applicationId, CancellationToken cancellationToken);
}
