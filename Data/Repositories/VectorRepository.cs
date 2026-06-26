using MoneyPenny.Models.Rag;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace MoneyPenny.Data.Repositories;

public class VectorRepository : IVectorRepository
{
    private readonly VectorDbContext _context;

    public VectorRepository(VectorDbContext context)
    {
        _context = context;
    }

    public async Task DeleteTicketIndexAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        await DeleteChunksByTicketAndSourceAsync(
            ticketId,
            DocumentChunkSource.TicketDocument,
            cancellationToken);
    }

    public async Task DeleteChunksBySourceAsync(
        DocumentChunkSource source,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.Source == source)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return;
        }

        _context.DocumentChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteChunksByTicketAndSourceAsync(
        int ticketId,
        DocumentChunkSource source,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.TicketId == ticketId && c.Source == source)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return;
        }

        _context.DocumentChunks.RemoveRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsTicketIndexedAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        return IsTicketIndexedBySourceAsync(ticketId, DocumentChunkSource.TicketDocument, cancellationToken);
    }

    public Task<bool> IsTicketIndexedBySourceAsync(
        int ticketId,
        DocumentChunkSource source,
        CancellationToken cancellationToken = default)
    {
        return _context.DocumentChunks
            .AsNoTracking()
            .AnyAsync(c => c.TicketId == ticketId && c.Source == source, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetIndexedTicketIdsAsync(CancellationToken cancellationToken = default)
    {
        return await GetIndexedTicketIdsBySourceAsync(DocumentChunkSource.TicketDocument, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetIndexedTicketIdsBySourceAsync(
        DocumentChunkSource source,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Source == source)
            .Select(c => c.TicketId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountIndexedTicketsBySourceAsync(
        DocumentChunkSource source,
        CancellationToken cancellationToken = default)
    {
        return _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Source == source)
            .Select(c => c.TicketId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task SaveChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        _context.DocumentChunks.AddRange(chunks);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveEmbeddingsAsync(IEnumerable<TicketEmbedding> embeddings, CancellationToken cancellationToken = default)
    {
        _context.TicketEmbeddings.AddRange(embeddings);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SimilarDocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        double minScore,
        int? ticketId = null,
        int? excludeTicketId = null,
        DocumentChunkSource? source = null,
        CancellationToken cancellationToken = default)
    {
        if (queryVector.Length == 0)
        {
            return Array.Empty<SimilarDocumentChunk>();
        }

        var sql = """
            SELECT
                dc."Id",
                dc."TicketId",
                dc."TicketNumber",
                dc."ChunkIndex",
                dc."Content",
                dc."CreatedAt",
                1 - (te."Vector" <=> @query) AS "Score"
            FROM ticket_embeddings te
            INNER JOIN document_chunks dc ON dc."Id" = te."DocumentChunkId"
            WHERE 1 - (te."Vector" <=> @query) >= @minScore
            """;

        if (ticketId is not null)
        {
            sql += """
                
                AND te."TicketId" = @ticketId
                """;
        }

        if (excludeTicketId is not null)
        {
            sql += """
                
                AND te."TicketId" <> @excludeTicketId
                """;
        }

        if (source is not null)
        {
            sql += """
                
                AND dc."Source" = @source
                """;
        }

        sql += """
            
            ORDER BY te."Vector" <=> @query
            LIMIT @topK
            """;

        await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;

            command.Parameters.Add(new NpgsqlParameter("query", new Vector(queryVector)));
            command.Parameters.Add(new NpgsqlParameter("minScore", NpgsqlDbType.Double) { Value = minScore });
            command.Parameters.Add(new NpgsqlParameter("topK", NpgsqlDbType.Integer) { Value = topK });

            if (ticketId is not null)
            {
                command.Parameters.Add(new NpgsqlParameter("ticketId", NpgsqlDbType.Integer) { Value = ticketId.Value });
            }

            if (excludeTicketId is not null)
            {
                command.Parameters.Add(new NpgsqlParameter("excludeTicketId", NpgsqlDbType.Integer) { Value = excludeTicketId.Value });
            }

            if (source is not null)
            {
                command.Parameters.Add(new NpgsqlParameter("source", NpgsqlDbType.Integer) { Value = (int)source.Value });
            }

            var results = new List<SimilarDocumentChunk>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SimilarDocumentChunk
                {
                    Chunk = new DocumentChunk
                    {
                        Id = reader.GetInt32(0),
                        TicketId = reader.GetInt32(1),
                        TicketNumber = reader.GetString(2),
                        ChunkIndex = reader.GetInt32(3),
                        Content = reader.GetString(4),
                        CreatedAt = reader.GetDateTime(5)
                    },
                    Score = reader.GetDouble(6)
                });
            }

            return results;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetChunksByTicketAndSourceAsync(
        int ticketId,
        DocumentChunkSource source,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.TicketId == ticketId && c.Source == source)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveQueryLogAsync(RagQueryLog log, CancellationToken cancellationToken = default)
    {
        _context.RagQueryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
