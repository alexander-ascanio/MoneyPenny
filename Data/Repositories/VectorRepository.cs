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
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentChunks.Where(c => c.Source == source);
        if (isKnowledgeBase is not null)
        {
            query = query.Where(c => c.IsKnowledgeBase == isKnowledgeBase.Value);
        }

        var chunks = await query.ToListAsync(cancellationToken);

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
        return await GetIndexedTicketIdsBySourceAsync(
            DocumentChunkSource.TicketDocument,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetIndexedTicketIdsBySourceAsync(
        DocumentChunkSource source,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Source == source);

        if (isKnowledgeBase is not null)
        {
            query = query.Where(c => c.IsKnowledgeBase == isKnowledgeBase.Value);
        }

        return await query
            .Select(c => c.TicketId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountIndexedTicketsBySourceAsync(
        DocumentChunkSource source,
        bool? isKnowledgeBase = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Source == source);

        if (isKnowledgeBase is not null)
        {
            query = query.Where(c => c.IsKnowledgeBase == isKnowledgeBase.Value);
        }

        return query
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

    public void ClearChangeTracker() => _context.ChangeTracker.Clear();

    public async Task<IReadOnlyList<SimilarDocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        double minScore,
        int? ticketId = null,
        int? excludeTicketId = null,
        DocumentChunkSource? source = null,
        bool? isKnowledgeBase = null,
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

        if (isKnowledgeBase is not null)
        {
            sql += """
                
                AND dc."IsKnowledgeBase" = @isKnowledgeBase
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

            if (isKnowledgeBase is not null)
            {
                command.Parameters.Add(new NpgsqlParameter("isKnowledgeBase", NpgsqlDbType.Boolean) { Value = isKnowledgeBase.Value });
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

    public async Task<RagQueryLog> SaveQueryLogAsync(
        RagQueryLog log,
        bool reuseIfUnrated = false,
        CancellationToken cancellationToken = default)
    {
        if (reuseIfUnrated)
        {
            if (log.ResponseType == RagResponseType.Context)
            {
                var existingContext = await _context.RagQueryLogs
                    .FirstOrDefaultAsync(
                        l => l.TicketId == log.TicketId
                             && l.ResponseType == RagResponseType.Context
                             && l.UserId == log.UserId
                             && l.Rating == null,
                        cancellationToken);

                if (existingContext is not null)
                {
                    existingContext.Question = log.Question;
                    existingContext.Answer = log.Answer;
                    existingContext.PromptVersion = log.PromptVersion;
                    existingContext.CreatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                    return existingContext;
                }
            }
            else
            {
                var existing = await _context.RagQueryLogs
                    .FirstOrDefaultAsync(
                        l => l.TicketId == log.TicketId
                             && l.ResponseType == log.ResponseType
                             && l.Answer == log.Answer
                             && l.UserId == log.UserId
                             && l.Rating == null,
                        cancellationToken);

                if (existing is not null)
                {
                    return existing;
                }
            }
        }

        _context.RagQueryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
        return log;
    }

    public async Task<bool> RateQueryLogAsync(
        int queryLogId,
        string userId,
        short rating,
        CancellationToken cancellationToken = default)
    {
        if (rating is not (RagQueryLog.RatingGood or RagQueryLog.RatingBad or RagQueryLog.RatingNotAnswerable or RagQueryLog.RatingClear))
        {
            return false;
        }

        var log = await _context.RagQueryLogs
            .FirstOrDefaultAsync(l => l.Id == queryLogId, cancellationToken);

        if (log is null)
        {
            return false;
        }

        if (rating == RagQueryLog.RatingClear)
        {
            log.Rating = null;
            log.RatedByUserId = null;
            log.RatedAt = null;
        }
        else
        {
            log.Rating = rating;
            log.RatedByUserId = userId;
            log.RatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RagQueryLog>> GetRatedQueryLogsByTicketAsync(
        int ticketId,
        RagResponseType responseType,
        CancellationToken cancellationToken = default)
    {
        return await _context.RagQueryLogs
            .AsNoTracking()
            .Where(l => l.TicketId == ticketId
                        && l.ResponseType == responseType
                        && l.Rating != null)
            .OrderByDescending(l => l.RatedAt)
            .ThenByDescending(l => l.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<RagQueryLog?> GetLatestQueryLogByTicketAsync(
        int ticketId,
        RagResponseType responseType,
        CancellationToken cancellationToken = default)
    {
        return _context.RagQueryLogs
            .AsNoTracking()
            .Where(l => l.TicketId == ticketId && l.ResponseType == responseType)
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasQueryLogForTicketAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return _context.RagQueryLogs
            .AsNoTracking()
            .AnyAsync(
                l => l.TicketId == ticketId && l.ResponseType == RagResponseType.Context,
                cancellationToken);
    }

    public Task<int> CountRatedQueryLogsAsync(
        RagResponseType? responseType = null,
        CancellationToken cancellationToken = default)
    {
        return BuildRatedQueryLogsQuery(responseType).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RagQueryLog>> GetRatedQueryLogsPageAsync(
        int skip,
        int take,
        RagResponseType? responseType = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildRatedQueryLogsQuery(responseType)
            .OrderByDescending(l => l.RatedAt)
            .ThenByDescending(l => l.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RagRatingMonthlyStatsRow>> GetRatingMonthlyStatsAsync(
        RagResponseType? responseType = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildRatedQueryLogsQuery(responseType)
            .Where(l => l.RatedAt != null)
            .GroupBy(l => new { l.RatedAt!.Value.Year, l.RatedAt!.Value.Month, l.Rating })
            .Select(g => new RagRatingMonthlyStatsRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Rating = g.Key.Rating!.Value,
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<RagQueryLog> BuildRatedQueryLogsQuery(RagResponseType? responseType)
    {
        var query = _context.RagQueryLogs
            .AsNoTracking()
            .Where(l => l.Rating != null && l.TicketId != null);

        if (responseType.HasValue)
        {
            query = query.Where(l => l.ResponseType == responseType.Value);
        }

        return query;
    }

    public async Task UpdateQueryLogTeamSupportActionIdAsync(
        int queryLogId,
        string? teamSupportActionId,
        CancellationToken cancellationToken = default)
    {
        var log = await _context.RagQueryLogs
            .FirstOrDefaultAsync(l => l.Id == queryLogId, cancellationToken);

        if (log is null)
        {
            return;
        }

        log.TeamSupportActionId = string.IsNullOrWhiteSpace(teamSupportActionId)
            ? null
            : teamSupportActionId.Trim();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
