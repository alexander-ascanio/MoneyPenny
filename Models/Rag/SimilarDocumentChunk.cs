namespace MoneyPenny.Models.Rag;

public class SimilarDocumentChunk
{
    public DocumentChunk Chunk { get; init; } = null!;
    public double Score { get; init; }
}
