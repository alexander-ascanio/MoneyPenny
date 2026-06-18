using Microsoft.Extensions.Configuration;
using Npgsql;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var config = new ConfigurationBuilder()
    .SetBasePath(projectRoot)
    .AddJsonFile("appsettings.json", optional: false)
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
Console.WriteLine($"Connected to: {section["Name"]} @ {section["Host"]}");

const string tablesSql = """
    SELECT table_schema, table_name
    FROM information_schema.tables
    WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
      AND table_type = 'BASE TABLE'
      AND (table_name ILIKE '%ticket%' OR table_name ILIKE '%action%' OR table_name ILIKE '%incident%')
    ORDER BY table_schema, table_name;
    """;

Console.WriteLine("\nTables:");
await using (var cmd = new NpgsqlCommand(tablesSql, conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        Console.WriteLine($"  {reader.GetString(0)}.{reader.GetString(1)}");
}

const string columnsSql = """
    SELECT table_name, column_name, data_type
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND (table_name ILIKE '%ticket%' OR table_name ILIKE '%action%')
    ORDER BY table_name, ordinal_position;
    """;

Console.WriteLine("\nColumns:");
await using (var cmd = new NpgsqlCommand(columnsSql, conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        Console.WriteLine($"  {reader.GetString(0)}.{reader.GetString(1)} ({reader.GetString(2)})");
}
