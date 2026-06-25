using Pgvector;

namespace MoneyPenny.Models.Rag;

public class TicketEmbedding
{
    public int Id { get; set; }
    public int DocumentChunkId { get; set; }
    public DocumentChunk DocumentChunk { get; set; } = null!;
    public int TicketId { get; set; }
    public string Model { get; set; } = string.Empty;
    public Vector Embedding { get; set; } = null!;
}
