using Microsoft.Extensions.Options;
using Npgsql;
using PulseTech.Ingestion.Worker.Mqtt;
using PulseTech.Ingestion.Worker.Telemetry;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<MqttOptions>()
    .Bind(builder.Configuration.GetSection(MqttOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<PostgresOptions>()
    .Bind(builder.Configuration.GetSection(PostgresOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.Configure<ApplicationSchemaOptions>(builder.Configuration.GetSection(ApplicationSchemaOptions.SectionName));

builder.Services.AddSingleton(sp =>
{
    var postgres = sp.GetRequiredService<IOptions<PostgresOptions>>().Value;
    var connectionStringBuilder = new NpgsqlConnectionStringBuilder
    {
        Host = postgres.Host,
        Port = postgres.Port,
        Database = postgres.Database,
        Username = postgres.Username,
        Password = postgres.Password
    };
    return NpgsqlDataSource.Create(connectionStringBuilder.ConnectionString);
});

builder.Services.AddSingleton<IApplicationSchemaResolver, ConfigurationApplicationSchemaResolver>();
builder.Services.AddSingleton<ICoreRegistryValidator, PostgresCoreRegistryValidator>();
builder.Services.AddSingleton<ITelemetryRepository, PostgresTelemetryRepository>();
builder.Services.AddHostedService<TelemetryIngestionService>();

var host = builder.Build();
host.Run();
