namespace PulseTech.Ingestion.Worker.Telemetry;

public interface ITelemetryRepository
{
    Task InsertAsync(ApplicationSchemaMapping mapping, TelemetryMessage message, CancellationToken cancellationToken);
}
