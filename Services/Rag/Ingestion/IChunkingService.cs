using MoneyPenny.Models.Rag;

namespace MoneyPenny.Services.Rag.Ingestion;

public interface IChunkingService
{
    IReadOnlyList<DocumentChunk> SplitIntoChunks(
        string text,
        int ticketId,
        string ticketNumber,
        DocumentChunkSource source = DocumentChunkSource.TicketDocument,
        int? ticketActionId = null,
        bool isKnowledgeBase = false);
}
