using Npgsql;
using PulseTech.Ingestion.Worker.Mqtt;
using PulseTech.Ingestion.Worker.Telemetry;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection(MqttOptions.SectionName));
builder.Services.Configure<ApplicationSchemaOptions>(builder.Configuration.GetSection(ApplicationSchemaOptions.SectionName));

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("Telemetry")
        ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Telemetry' configuration.");
    return NpgsqlDataSource.Create(connectionString);
});

builder.Services.AddSingleton<IApplicationSchemaResolver, ConfigurationApplicationSchemaResolver>();
builder.Services.AddSingleton<ICoreRegistryValidator, PostgresCoreRegistryValidator>();
builder.Services.AddSingleton<ITelemetryRepository, PostgresTelemetryRepository>();
builder.Services.AddHostedService<TelemetryIngestionService>();

var host = builder.Build();
host.Run();
