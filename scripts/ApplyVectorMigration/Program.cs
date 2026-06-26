using Microsoft.Extensions.Configuration;
using Npgsql;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var config = new ConfigurationBuilder()
    .SetBasePath(projectRoot)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var section = config.GetSection("VectorDatabase");
var cs = new NpgsqlConnectionStringBuilder
{
    Host = section["Host"] ?? "localhost",
    Port = int.Parse(section["Port"] ?? "5432"),
    Database = section["Name"] ?? "moneypenny_vectors_db",
    Username = section["User"] ?? "postgres",
    Password = section["Password"] ?? string.Empty,
    SslMode = Enum.Parse<SslMode>(section["SslMode"] ?? "Disable")
}.ConnectionString;

const string migrationId = "20260625140000_AddDocumentChunkSource";

await using var conn = new NpgsqlConnection(cs);
await conn.OpenAsync();

Console.WriteLine($"Conectado a {section["Name"]} @ {section["Host"]}");

await using (var check = new NpgsqlCommand(
    """
    SELECT 1 FROM information_schema.columns
    WHERE table_name = 'document_chunks' AND column_name = 'Source'
    """, conn))
{
    var exists = await check.ExecuteScalarAsync() is not null;
    if (!exists)
    {
        Console.WriteLine("Añadiendo columnas Source y TicketActionId...");
        await using var tx = await conn.BeginTransactionAsync();
        await ExecAsync(conn, """
            ALTER TABLE document_chunks
            ADD COLUMN "Source" integer NOT NULL DEFAULT 0
            """, tx);
        await ExecAsync(conn, """
            ALTER TABLE document_chunks
            ADD COLUMN "TicketActionId" integer NULL
            """, tx);
        await ExecAsync(conn, """
            CREATE INDEX "IX_document_chunks_Source" ON document_chunks ("Source")
            """, tx);
        await ExecAsync(conn, """
            CREATE INDEX "IX_document_chunks_TicketId_Source" ON document_chunks ("TicketId", "Source")
            """, tx);
        await tx.CommitAsync();
        Console.WriteLine("Columnas e índices creados.");
    }
    else
    {
        Console.WriteLine("La columna Source ya existe.");
    }
}

await using (var history = new NpgsqlCommand(
    """
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES (@id, '10.0.3')
    ON CONFLICT ("MigrationId") DO NOTHING
    """, conn))
{
    history.Parameters.AddWithValue("id", migrationId);
    var rows = await history.ExecuteNonQueryAsync();
    Console.WriteLine(rows > 0
        ? $"Migración {migrationId} registrada en historial."
        : $"Migración {migrationId} ya estaba en historial.");
}

static async Task ExecAsync(NpgsqlConnection conn, string sql, NpgsqlTransaction tx)
{
    await using var cmd = new NpgsqlCommand(sql, conn, tx);
    await cmd.ExecuteNonQueryAsync();
}
