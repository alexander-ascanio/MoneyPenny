using MoneyPenny.Options;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MoneyPenny.Data;

/// <summary>
/// En Development: prueba TicketsDatabase (Azure) y, si no responde, usa el fallback local.
/// </summary>
public static class TicketsDatabaseConnectionResolver
{
    private const int ProbeTimeoutSeconds = 5;

    public static TicketsDatabaseOptions Resolve(
        TicketsDatabaseOptions primary,
        IHostEnvironment environment,
        ILogger? logger = null)
    {
        if (!environment.IsDevelopment()
            || !primary.UseLocalFallbackOnConnectionFailure
            || string.IsNullOrWhiteSpace(primary.FallbackName))
        {
            return primary;
        }

        var primaryCs = PostgresConnectionHelper.BuildConnectionString(primary, ProbeTimeoutSeconds);
        if (CanConnect(primaryCs, logger, primary.Host, primary.Name))
        {
            logger?.LogInformation(
                "TicketsDatabase: conectado a {Host}/{Database}.",
                primary.Host,
                primary.Name);
            return primary;
        }

        var fallback = CreateFallbackOptions(primary);
        var fallbackCs = PostgresConnectionHelper.BuildConnectionString(fallback, ProbeTimeoutSeconds);
        if (CanConnect(fallbackCs, logger, fallback.Host, fallback.Name))
        {
            logger?.LogWarning(
                "TicketsDatabase: no se pudo conectar a {PrimaryHost}/{PrimaryDatabase}. "
                + "Usando fallback local {FallbackHost}/{FallbackDatabase}.",
                primary.Host,
                primary.Name,
                fallback.Host,
                fallback.Name);
            return fallback;
        }

        logger?.LogError(
            "TicketsDatabase: falló la conexión a Azure ({PrimaryHost}/{PrimaryDatabase}) "
            + "y también al fallback local ({FallbackHost}/{FallbackDatabase}).",
            primary.Host,
            primary.Name,
            fallback.Host,
            fallback.Name);

        // Devolver el fallback para que los mensajes de error apunten a local; la app seguirá fallando al consultar.
        return fallback;
    }

    private static TicketsDatabaseOptions CreateFallbackOptions(TicketsDatabaseOptions primary) =>
        new()
        {
            Host = string.IsNullOrWhiteSpace(primary.FallbackHost) ? "localhost" : primary.FallbackHost,
            Port = primary.FallbackPort > 0 ? primary.FallbackPort : 5432,
            Name = primary.FallbackName,
            User = string.IsNullOrWhiteSpace(primary.FallbackUser) ? primary.User : primary.FallbackUser,
            Password = string.IsNullOrWhiteSpace(primary.FallbackPassword) ? primary.Password : primary.FallbackPassword,
            SslMode = string.IsNullOrWhiteSpace(primary.FallbackSslMode) ? "Disable" : primary.FallbackSslMode,
            TrustServerCertificate = primary.TrustServerCertificate,
            ApplyMigrationsOnStartup = false,
            EnableSeed = false,
            UseLocalFallbackOnConnectionFailure = false
        };

    private static bool CanConnect(string connectionString, ILogger? logger, string host, string database)
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = new NpgsqlCommand("SELECT 1", connection);
            command.ExecuteScalar();
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "TicketsDatabase: probe fallido en {Host}/{Database}.",
                host,
                database);
            return false;
        }
    }
}
