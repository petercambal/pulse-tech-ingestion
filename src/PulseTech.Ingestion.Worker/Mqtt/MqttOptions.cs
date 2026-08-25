namespace PulseTech.Ingestion.Worker.Mqtt;

public sealed class MqttOptions
{
    public const string SectionName = "Mqtt";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1883;
    public string ClientId { get; init; } = "pulsetech-ingestion";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool UseTls { get; init; }

    /// <summary>Wildcard subscription covering every application/device: pulsetech/+/+/telemetry</summary>
    public string TopicFilter { get; init; } = "pulsetech/+/+/telemetry";

    public int ReconnectDelaySeconds { get; init; } = 5;
}
