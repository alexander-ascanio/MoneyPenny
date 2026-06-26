namespace MoneyPenny.Models.Rag;

public class DocumentChunk
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public int? TicketActionId { get; set; }
    public DocumentChunkSource Source { get; set; } = DocumentChunkSource.TicketDocument;
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
