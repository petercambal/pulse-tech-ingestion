namespace PulseTech.Ingestion.Worker.Telemetry;

/// <summary>
/// A telemetry message ready to be persisted, resolved from an inbound MQTT publish.
/// </summary>
public sealed record TelemetryMessage(
    string ApplicationName,
    string DeviceId,
    DateTimeOffset ReceivedAt,
    string RawPayload);
