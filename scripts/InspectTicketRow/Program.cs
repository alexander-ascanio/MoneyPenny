using Microsoft.Extensions.Configuration;
using Npgsql;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var config = new ConfigurationBuilder()
    .SetBasePath(projectRoot)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var section = config.GetSection("TicketsDatabase");
var cs = new NpgsqlConnectionStringBuilder
{
    Host = section["Host"]!,
    Port = int.Parse(section["Port"] ?? "5432"),
    Database = section["Name"]!,
    Username = section["User"]!,
    Password = section["Password"]!,
    SslMode = Enum.Parse<SslMode>(section["SslMode"] ?? "Require")
}.ConnectionString;

await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();
const string sql = """SELECT * FROM tickets WHERE "Id" = 24401;""";
await using var cmd = new NpgsqlCommand(sql, conn);
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    for (var i = 0; i < reader.FieldCount; i++)
    {
        var value = reader.IsDBNull(i) ? "(null)" : reader.GetValue(i)?.ToString();
        if (value?.Length > 150) value = value[..150] + "...";
        Console.WriteLine($"{reader.GetName(i)}: {value}");
    }
}
