using Microsoft.Extensions.Configuration;
using Npgsql;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var ticketIdArg = args.Length > 0 ? args[0] : "24401";

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

if (ticketIdArg.Equals("sample-with-attachment", StringComparison.OrdinalIgnoreCase))
{
    const string sampleSql = """
        SELECT t."Id", t."TicketNumber", ta."Id", position('attachment' in lower(ta."Content")) AS pos
        FROM ticket_actions ta
        JOIN tickets t ON t."Id" = ta."TicketId"
        WHERE position('attachment' in lower(coalesce(ta."Content", ''))) > 0
        ORDER BY ta."CreatedAt" DESC
        LIMIT 3;
        """;
    await using var sampleCmd = new NpgsqlCommand(sampleSql, conn);
    await using var sampleReader = await sampleCmd.ExecuteReaderAsync();
    Console.WriteLine("Sample actions with 'attachment' in content:");
    while (await sampleReader.ReadAsync())
        Console.WriteLine($"  ticket {sampleReader.GetInt32(0)} (#{sampleReader.GetString(1)}) action {sampleReader.GetInt32(2)} pos={sampleReader.GetInt32(3)}");
    return;
}

int ticketId;
if (int.TryParse(ticketIdArg, out var parsedId))
{
    ticketId = parsedId;
}
else
{
    const string byNumberSql = """SELECT "Id" FROM tickets WHERE "TicketNumber" = @num LIMIT 1;""";
    await using var lookup = new NpgsqlCommand(byNumberSql, conn);
    lookup.Parameters.AddWithValue("num", ticketIdArg);
    var lookupId = await lookup.ExecuteScalarAsync();
    if (lookupId is int foundId)
        ticketId = foundId;
    else
    {
        Console.WriteLine($"Ticket {ticketIdArg} not found");
        return;
    }
}

const string ticketSql = """
    SELECT "Id", "TicketNumber", "TeamSupportId", length("Description") AS desc_len,
           left("Description", 500) AS desc_preview,
           position('attachment' in lower("Description")) AS desc_has_attachment,
           position('teamsupport' in lower("Description")) AS desc_has_ts
    FROM tickets WHERE "Id" = @id;
    """;

await using (var cmd = new NpgsqlCommand(ticketSql, conn))
{
    cmd.Parameters.AddWithValue("id", ticketId);
    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
    {
        Console.WriteLine($"Ticket {ticketId} not found");
        return;
    }

    Console.WriteLine($"Ticket Id={reader.GetInt32(0)} Number={reader.GetString(1)} TS={reader.GetString(2)}");
    Console.WriteLine($"Description len={reader.GetInt64(3)} attachment_pos={reader.GetInt32(5)} ts_pos={reader.GetInt32(6)}");
    Console.WriteLine($"Description preview:\n{reader.GetString(4)}\n");
}

const string actionsSql = """
    SELECT "Id", "ActionType", "CreatedByName", "CreatedAt", "IsVisible",
           length("Content") AS content_len,
           position('attachment' in lower(coalesce("Content", ''))) AS has_attachment,
           position('teamsupport' in lower(coalesce("Content", ''))) AS has_ts,
           position('captura' in lower(coalesce("Content", ''))) AS has_captura,
           position('<img' in lower(coalesce("Content", ''))) AS has_img,
           position('.png' in lower(coalesce("Content", ''))) AS has_png,
           left("Content", 800) AS content_preview
    FROM ticket_actions
    WHERE "TicketId" = @id
    ORDER BY "CreatedAt";
    """;

Console.WriteLine("Actions:");
await using (var cmd = new NpgsqlCommand(actionsSql, conn))
{
    cmd.Parameters.AddWithValue("id", ticketId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var actionType = reader.IsDBNull(1) ? "?" : reader.GetString(1);
        var createdBy = reader.IsDBNull(2) ? "?" : reader.GetString(2);
        var preview = reader.IsDBNull(11) ? "(null)" : reader.GetString(11);
        Console.WriteLine($"--- Action Id={reader.GetInt32(0)} Type={actionType} By={createdBy} Visible={reader.GetBoolean(4)}");
        Console.WriteLine($"    len={reader.GetInt64(5)} attachment={reader.GetInt32(6)} ts={reader.GetInt32(7)} captura={reader.GetInt32(8)} img={reader.GetInt32(9)} png={reader.GetInt32(10)}");
        Console.WriteLine($"    preview:\n{preview}\n");
    }
}

const string actionMetaSql = """
    SELECT "Id", "TeamSupportActionId", "ActionType", length("Content"),
           position('app.na3.teamsupport.com' in coalesce("Content", '')) AS has_ts_url
    FROM ticket_actions
    WHERE "TicketId" = @id
    ORDER BY "CreatedAt";
    """;

Console.WriteLine("Action metadata:");
await using (var cmd = new NpgsqlCommand(actionMetaSql, conn))
{
    cmd.Parameters.AddWithValue("id", ticketId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var tsActionId = reader.IsDBNull(1) ? "(null)" : reader.GetString(1);
        Console.WriteLine($"  Id={reader.GetInt32(0)} TSActionId={tsActionId} Type={reader.GetString(2)} len={reader.GetInt64(3)} ts_url={reader.GetInt32(4)}");
    }
}

const string tsUrlSql = """
    SELECT "Id",
           substring("Content" from position('app.na3.teamsupport.com' in "Content") for 250) AS ts_url
    FROM ticket_actions
    WHERE "TicketId" = @id AND position('app.na3.teamsupport.com' in coalesce("Content", '')) > 0;
    """;

Console.WriteLine("TeamSupport URL in content:");
await using (var cmd = new NpgsqlCommand(tsUrlSql, conn))
{
    cmd.Parameters.AddWithValue("id", ticketId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
        Console.WriteLine($"Action {reader.GetInt32(0)}:\n{reader.GetString(1)}\n");
}

const string fullContentSql = """
    SELECT "Id", "Content"
    FROM ticket_actions
    WHERE "TicketId" = @id
    ORDER BY "CreatedAt";
    """;

Console.WriteLine("Full content:");
await using (var cmd = new NpgsqlCommand(fullContentSql, conn))
{
    cmd.Parameters.AddWithValue("id", ticketId);
    await using var reader = await cmd.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        var content = reader.IsDBNull(1) ? "" : reader.GetString(1);
        Console.WriteLine($"=== Action {reader.GetInt32(0)} (len={content.Length}) ===");
        Console.WriteLine(content);
        var decoded = System.Net.WebUtility.HtmlDecode(content);
        foreach (var marker in new[] { "teamsupport.com", "/attachments/", ".png", "<img" })
        {
            var idx = decoded.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = Math.Max(0, idx - 80);
                var len = Math.Min(400, decoded.Length - start);
                Console.WriteLine($"--- '{marker}' snippet ---\n{decoded.Substring(start, len)}\n");
            }
        }
        Console.WriteLine();
    }
}
