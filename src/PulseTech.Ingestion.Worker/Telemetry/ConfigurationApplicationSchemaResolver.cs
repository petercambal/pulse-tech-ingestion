using Microsoft.Extensions.Options;

namespace PulseTech.Ingestion.Worker.Telemetry;

public sealed class ConfigurationApplicationSchemaResolver : IApplicationSchemaResolver
{
    private readonly ApplicationSchemaOptions _options;

    public ConfigurationApplicationSchemaResolver(IOptions<ApplicationSchemaOptions> options)
    {
        _options = options.Value;
        _options.Validate();
    }

    public bool TryResolve(string applicationName, out ApplicationSchemaMapping mapping)
        => _options.Applications.TryGetValue(applicationName, out mapping!);
}
