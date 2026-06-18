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
}

public class VectorDatabaseOptions : PostgresDatabaseOptions
{
    public const string SectionName = "VectorDatabase";
}
