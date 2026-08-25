namespace PulseTech.Ingestion.Worker.Telemetry;

/// <summary>
/// Parsed representation of a "pulsetech/{application_id}/{device_id}/telemetry" MQTT topic.
/// Both segments are raw uuid strings — core.applications.id and core.devices.id respectively —
/// not human-readable codes/names.
/// </summary>
public sealed record TelemetryTopic(string ApplicationName, string DeviceId)
{
    private const string RootSegment = "pulsetech";
    private const string LeafSegment = "telemetry";

    public static bool TryParse(string topic, out TelemetryTopic? result)
    {
        result = null;

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 4)
        {
            return false;
        }

        if (!segments[0].Equals(RootSegment, StringComparison.OrdinalIgnoreCase) ||
            !segments[3].Equals(LeafSegment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        result = new TelemetryTopic(segments[1], segments[2]);
        return true;
    }
}
