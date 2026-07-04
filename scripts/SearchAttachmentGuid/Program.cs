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

const string guid = "19325532-f10d-4224-b9c5-0751daa4833e";

await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();

const string columnsSql = """
    SELECT table_name, column_name
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND data_type IN ('text', 'character varying')
    ORDER BY table_name, column_name;
    """;

var columns = new List<(string Table, string Column)>();
await using (var cmd = new NpgsqlCommand(columnsSql, conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        columns.Add((reader.GetString(0), reader.GetString(1)));
}

Console.WriteLine($"Searching {columns.Count} text columns for attachment guid...");
foreach (var (table, column) in columns)
{
    var sql = $"""SELECT COUNT(*) FROM "{table}" WHERE "{column}" ILIKE @pattern;""";
    await using var cmd = new NpgsqlCommand(sql, conn);
    cmd.Parameters.AddWithValue("pattern", $"%{guid}%");
    var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    if (count > 0)
        Console.WriteLine($"  {table}.{column}: {count}");
}

const string actionSql = """
    SELECT "Id", "TeamSupportActionId", length("Content"),
           position('adjunto' in lower("Content")) > 0 AS mentions_adjunto
    FROM ticket_actions WHERE "TicketId" = 24401;
    """;
await using (var cmd = new NpgsqlCommand(actionSql, conn))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    while (await reader.ReadAsync())
        Console.WriteLine($"Action {reader.GetInt32(0)} TS={reader.GetString(1)} len={reader.GetInt64(2)} adjunto={reader.GetBoolean(3)}");
}
