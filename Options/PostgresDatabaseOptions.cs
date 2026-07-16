namespace MoneyPenny.Options;

public class PostgresDatabaseOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Name { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SslMode { get; set; } = "Disable";
    public bool TrustServerCertificate { get; set; } = true;
    public bool ApplyMigrationsOnStartup { get; set; }
    public bool EnableSeed { get; set; }
}

public class ApplicationDatabaseOptions : PostgresDatabaseOptions
{
    public const string SectionName = "Database";
}

public class TicketsDatabaseOptions : PostgresDatabaseOptions
{
    public const string SectionName = "TicketsDatabase";

    /// <summary>
    /// Solo Development: si falla la conexión primaria (p. ej. Azure), usar Fallback*.
    /// </summary>
    public bool UseLocalFallbackOnConnectionFailure { get; set; }

    public string FallbackHost { get; set; } = "localhost";
    public int FallbackPort { get; set; } = 5432;
    public string FallbackName { get; set; } = "teamsupport_local_db";
    public string FallbackUser { get; set; } = "postgres";
    public string FallbackPassword { get; set; } = string.Empty;
    public string FallbackSslMode { get; set; } = "Disable";
}

public class VectorDatabaseOptions : PostgresDatabaseOptions
{
    public const string SectionName = "VectorDatabase";
}
