using System.Text.RegularExpressions;

namespace PulseTech.Ingestion.Worker.Telemetry;

/// <summary>
/// Maps a core.applications.app_code to the Postgres schema/table that stores its telemetry. The
/// MQTT topic itself only carries core.applications.id (uuid); <see cref="ICoreRegistryValidator"/>
/// resolves that to app_code, which is then looked up here. Each application owns its own telemetry
/// table (with its own column shape), so this registry is the single place that grows as new
/// applications/sources are onboarded.
/// </summary>
public sealed class ApplicationSchemaOptions
{
    public const string SectionName = "TelemetryRouting";

    private static readonly Regex IdentifierPattern = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    /// <summary>Keyed by core.applications.app_code (case-insensitive).</summary>
    public Dictionary<string, ApplicationSchemaMapping> Applications { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public void Validate()
    {
        foreach (var (applicationId, mapping) in Applications)
        {
            if (!IdentifierPattern.IsMatch(mapping.Schema))
            {
                throw new InvalidOperationException(
                    $"Invalid schema name '{mapping.Schema}' configured for application '{applicationId}'.");
            }

            if (!IdentifierPattern.IsMatch(mapping.Table))
            {
                throw new InvalidOperationException(
                    $"Invalid table name '{mapping.Table}' configured for application '{applicationId}'.");
            }

            var hasColumns = mapping.Columns is { Length: > 0 };
            var hasPayloadColumn = !string.IsNullOrEmpty(mapping.PayloadColumn);

            if (hasColumns == hasPayloadColumn)
            {
                throw new InvalidOperationException(
                    $"Application '{applicationId}' must configure exactly one of 'Columns' (explicit typed columns) " +
                    $"or 'PayloadColumn' (single jsonb column), not both/neither.");
            }

            if (hasColumns)
            {
                foreach (var column in mapping.Columns!)
                {
                    if (!IdentifierPattern.IsMatch(column))
                    {
                        throw new InvalidOperationException(
                            $"Invalid column name '{column}' configured for application '{applicationId}'.");
                    }
                }
            }
            else if (!IdentifierPattern.IsMatch(mapping.PayloadColumn!))
            {
                throw new InvalidOperationException(
                    $"Invalid payload column name '{mapping.PayloadColumn}' configured for application '{applicationId}'.");
            }
        }
    }
}

/// <summary>
/// Either <see cref="Columns"/> (payload JSON keys are mapped 1:1 to typed table columns, e.g. smart-pot's
/// soil_moisture/light_lux/air_temp_c) or <see cref="PayloadColumn"/> (the whole payload is stored as-is in
/// a single jsonb column) must be set, never both.
/// </summary>
public sealed class ApplicationSchemaMapping
{
    // Plain class with property-only binding, not a positional record: ConfigurationBinder double-binds
    // array properties on types that have both a parameterized constructor and settable properties
    // (constructor pass + property pass), appending the config values a second time instead of replacing them.
    public required string Schema { get; init; }
    public required string Table { get; init; }
    public string[]? Columns { get; init; }
    public string? PayloadColumn { get; init; }
}
