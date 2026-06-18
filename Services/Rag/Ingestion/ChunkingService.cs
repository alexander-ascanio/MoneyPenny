using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Ingestion;

public class ChunkingService : IChunkingService
{
    private readonly RagOptions _options;

    public ChunkingService(IOptions<RagOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<DocumentChunk> SplitIntoChunks(string text, int ticketId, string ticketNumber)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<DocumentChunk>();
        var chunkSize = Math.Max(100, _options.ChunkSize);
        var overlap = Math.Clamp(_options.ChunkOverlap, 0, chunkSize / 2);
        var index = 0;

        for (var start = 0; start < text.Length; start += chunkSize - overlap)
        {
            var length = Math.Min(chunkSize, text.Length - start);
            chunks.Add(new DocumentChunk
            {
                TicketId = ticketId,
                TicketNumber = ticketNumber,
                ChunkIndex = index++,
                Content = text.Substring(start, length),
                CreatedAt = DateTime.UtcNow
            });

            if (start + length >= text.Length)
            {
                break;
            }
        }

        return chunks;
    }
}
