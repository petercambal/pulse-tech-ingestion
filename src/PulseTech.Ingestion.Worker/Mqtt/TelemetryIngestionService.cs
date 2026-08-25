using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using PulseTech.Ingestion.Worker.Telemetry;

namespace PulseTech.Ingestion.Worker.Mqtt;

/// <summary>
/// Subscribes to pulsetech/{application_id}/{device_id}/telemetry (both uuids), validates them
/// against core.applications/core.devices, resolves the destination table for the application, and
/// writes the payload to Postgres. Adding a new source/application only requires a new entry in
/// TelemetryRouting configuration — no code change here.
/// </summary>
public sealed class TelemetryIngestionService : BackgroundService
{
    private readonly MqttOptions _mqttOptions;
    private readonly IApplicationSchemaResolver _schemaResolver;
    private readonly ICoreRegistryValidator _registryValidator;
    private readonly ITelemetryRepository _repository;
    private readonly ILogger<TelemetryIngestionService> _logger;
    private readonly IManagedMqttClient _mqttClient;

    public TelemetryIngestionService(
        IOptions<MqttOptions> mqttOptions,
        IApplicationSchemaResolver schemaResolver,
        ICoreRegistryValidator registryValidator,
        ITelemetryRepository repository,
        ILogger<TelemetryIngestionService> logger)
    {
        _mqttOptions = mqttOptions.Value;
        _schemaResolver = schemaResolver;
        _registryValidator = registryValidator;
        _repository = repository;
        _logger = logger;
        _mqttClient = new MqttFactory().CreateManagedMqttClient();
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var clientOptionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(_mqttOptions.ClientId)
            .WithTcpServer(_mqttOptions.Host, _mqttOptions.Port)
            .WithCleanSession();

        if (_mqttOptions.UseTls)
        {
            clientOptionsBuilder.WithTlsOptions(tls => tls.UseTls());
        }

        if (!string.IsNullOrEmpty(_mqttOptions.Username))
        {
            clientOptionsBuilder.WithCredentials(_mqttOptions.Username, _mqttOptions.Password);
        }

        var managedOptions = new ManagedMqttClientOptionsBuilder()
            .WithAutoReconnectDelay(TimeSpan.FromSeconds(_mqttOptions.ReconnectDelaySeconds))
            .WithClientOptions(clientOptionsBuilder.Build())
            .Build();

        await _mqttClient.StartAsync(managedOptions);
        await _mqttClient.SubscribeAsync(_mqttOptions.TopicFilter);

        _logger.LogInformation(
            "MQTT ingestion started, subscribed to '{TopicFilter}' on {Host}:{Port}",
            _mqttOptions.TopicFilter, _mqttOptions.Host, _mqttOptions.Port);

        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _mqttClient.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;

        if (!TelemetryTopic.TryParse(topic, out var parsedTopic))
        {
            _logger.LogWarning("Ignoring message on unrecognized topic '{Topic}'", topic);
            await args.AcknowledgeAsync(CancellationToken.None);
            return;
        }

        // Not transient: a malformed/unknown application or device id won't become valid on
        // redelivery, so each check below logs and acknowledges rather than retrying forever.
        if (!Guid.TryParse(parsedTopic!.ApplicationName, out var applicationId) ||
            !Guid.TryParse(parsedTopic.DeviceId, out var deviceId))
        {
            _logger.LogWarning("Non-uuid application/device id in topic '{Topic}'; dropping message", topic);
            await args.AcknowledgeAsync(CancellationToken.None);
            return;
        }

        var appCode = await _registryValidator.ResolveApplicationCodeAsync(applicationId, CancellationToken.None);
        if (appCode is null)
        {
            _logger.LogWarning(
                "Unknown application_id '{ApplicationId}' (topic '{Topic}'); dropping message",
                applicationId, topic);
            await args.AcknowledgeAsync(CancellationToken.None);
            return;
        }

        if (!_schemaResolver.TryResolve(appCode, out var mapping))
        {
            _logger.LogWarning(
                "No telemetry routing configured for application '{AppCode}' (topic '{Topic}'); dropping message",
                appCode, topic);
            await args.AcknowledgeAsync(CancellationToken.None);
            return;
        }

        if (!await _registryValidator.DeviceBelongsToApplicationAsync(deviceId, applicationId, CancellationToken.None))
        {
            _logger.LogWarning(
                "Device '{DeviceId}' is unknown, inactive, or does not belong to application '{AppCode}' (topic '{Topic}'); dropping message",
                deviceId, appCode, topic);
            await args.AcknowledgeAsync(CancellationToken.None);
            return;
        }

        var payload = args.ApplicationMessage.PayloadSegment.Count == 0
            ? "{}"
            : System.Text.Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);

        var message = new TelemetryMessage(
            appCode,
            deviceId.ToString(),
            DateTimeOffset.UtcNow,
            payload);

        try
        {
            await _repository.InsertAsync(mapping, message, CancellationToken.None);
            await args.AcknowledgeAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist telemetry for application '{AppCode}', device '{DeviceId}'",
                appCode, deviceId);
            // Left unacknowledged so a QoS 1/2 broker redelivers; message is not lost on transient DB errors.
        }
    }

    public override void Dispose()
    {
        _mqttClient.Dispose();
        base.Dispose();
    }
}
