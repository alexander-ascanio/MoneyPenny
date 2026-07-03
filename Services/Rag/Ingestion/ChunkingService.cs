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

    public IReadOnlyList<DocumentChunk> SplitIntoChunks(
        string text,
        int ticketId,
        string ticketNumber,
        DocumentChunkSource source = DocumentChunkSource.TicketDocument,
        int? ticketActionId = null,
        bool isKnowledgeBase = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var chunks = new List<DocumentChunk>();
        var chunkSize = Math.Max(100, _options.ChunkSize);
        var overlap = Math.Clamp(_options.ChunkOverlap, 0, chunkSize / 2);
        var index = 0;
        var sanitizedText = text.Replace('\0', ' ');
        var safeTicketNumber = ticketNumber.Length <= 50
            ? ticketNumber
            : ticketNumber[..50];

        for (var start = 0; start < sanitizedText.Length; start += chunkSize - overlap)
        {
            var length = Math.Min(chunkSize, sanitizedText.Length - start);
            chunks.Add(new DocumentChunk
            {
                TicketId = ticketId,
                TicketNumber = safeTicketNumber,
                TicketActionId = ticketActionId,
                Source = source,
                IsKnowledgeBase = isKnowledgeBase,
                ChunkIndex = index++,
                Content = sanitizedText.Substring(start, length),
                CreatedAt = DateTime.UtcNow
            });

            if (start + length >= sanitizedText.Length)
            {
                break;
            }
        }

        return chunks;
    }

    public static string ReassembleChunkContents(
        IReadOnlyList<DocumentChunk> chunks,
        int chunkSize,
        int chunkOverlap)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var ordered = chunks.OrderBy(chunk => chunk.ChunkIndex).ToList();
        if (ordered.Count == 1)
        {
            return ordered[0].Content;
        }

        chunkSize = Math.Max(100, chunkSize);
        chunkOverlap = Math.Clamp(chunkOverlap, 0, chunkSize / 2);
        var step = chunkSize - chunkOverlap;

        var result = new System.Text.StringBuilder(ordered[0].Content);
        for (var i = 1; i < ordered.Count; i++)
        {
            var skip = Math.Max(0, ordered[i - 1].Content.Length - step);
            skip = Math.Min(skip, ordered[i].Content.Length);
            if (skip < ordered[i].Content.Length)
            {
                result.Append(ordered[i].Content.AsSpan(skip));
            }
        }

        return result.ToString();
    }
}
