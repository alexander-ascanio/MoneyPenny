using MoneyPenny.Models.Rag;
using Microsoft.EntityFrameworkCore;

namespace MoneyPenny.Data.Repositories;

public class VectorRepository : IVectorRepository
{
    private readonly VectorDbContext _context;

    public VectorRepository(VectorDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsTicketIndexedAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        return _context.DocumentChunks
            .AsNoTracking()
            .AnyAsync(c => c.TicketId == ticketId, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetIndexedTicketIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DocumentChunks
            .AsNoTracking()
            .Select(c => c.TicketId)
            .Distinct()
            .ToListAsync(cancellationToken);
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

    public Task<IReadOnlyList<DocumentChunk>> SearchSimilarAsync(
        float[] queryVector,
        int topK,
        CancellationToken cancellationToken = default)
    {
        // TODO: implementar búsqueda con pgvector
        IReadOnlyList<DocumentChunk> empty = Array.Empty<DocumentChunk>();
        return Task.FromResult(empty);
    }

    public async Task SaveQueryLogAsync(RagQueryLog log, CancellationToken cancellationToken = default)
    {
        _context.RagQueryLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
