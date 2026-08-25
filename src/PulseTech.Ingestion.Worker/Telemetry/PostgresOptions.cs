using System.ComponentModel.DataAnnotations;

namespace PulseTech.Ingestion.Worker.Telemetry;

public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; init; } = "";

    public int Port { get; init; } = 5432;

    [Required(AllowEmptyStrings = false)]
    public string Database { get; init; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Username { get; init; } = "";

    [Required(AllowEmptyStrings = false)]
    public string Password { get; init; } = "";
}
