using MoneyPenny.Options;
using Npgsql;

namespace MoneyPenny.Data;

public static class PostgresConnectionHelper
{
    public static string BuildConnectionString(PostgresDatabaseOptions options)
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

        return builder.ConnectionString;
    }
}
