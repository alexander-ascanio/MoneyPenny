using MoneyPenny.Options;
using Npgsql;

namespace MoneyPenny.Data;

public static class PostgresConnectionHelper
{
    public static string BuildConnectionString(
        PostgresDatabaseOptions options,
        int? timeoutSeconds = null)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = options.Host,
            Port = options.Port,
            Database = options.Name,
            Username = options.User,
            Password = options.Password,
            SslMode = Enum.Parse<SslMode>(options.SslMode ?? "Disable")
        };

        if (timeoutSeconds is > 0)
        {
            builder.Timeout = timeoutSeconds.Value;
        }

        return builder.ConnectionString;
    }
}
