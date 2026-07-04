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

const string sql = """SELECT * FROM ticket_actions WHERE "Id" = 194159;""";
await using var cmd = new NpgsqlCommand(sql, conn);
await using var reader = await cmd.ExecuteReaderAsync();
if (!await reader.ReadAsync())
{
    Console.WriteLine("Not found");
    return;
}

for (var i = 0; i < reader.FieldCount; i++)
{
    var name = reader.GetName(i);
    var value = reader.IsDBNull(i) ? "(null)" : reader.GetValue(i)?.ToString();
    if (value?.Length > 200) value = value[..200] + "...";
    Console.WriteLine($"{name}: {value}");
}
