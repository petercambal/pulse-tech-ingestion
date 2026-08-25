namespace PulseTech.Ingestion.Worker.Telemetry;

public interface IApplicationSchemaResolver
{
    /// <summary>
    /// Resolves the schema/table that owns telemetry for the given application_name.
    /// Returns false when the application is not (yet) registered, so callers can skip/park the message.
    /// </summary>
    bool TryResolve(string applicationName, out ApplicationSchemaMapping mapping);
}
