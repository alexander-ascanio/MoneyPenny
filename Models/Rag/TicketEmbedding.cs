namespace MoneyPenny.Models.Rag;

public class TicketEmbedding
{
    public int Id { get; set; }
    public int DocumentChunkId { get; set; }
    public DocumentChunk DocumentChunk { get; set; } = null!;
    public int TicketId { get; set; }
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Vector de embedding. Sustituir por pgvector cuando se añada la extensión.
    /// </summary>
    public float[] Vector { get; set; } = [];
}
