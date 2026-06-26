namespace MoneyPenny.ViewModels.Rag;

public class RagTokenPricingConfigViewModel
{
    public int CharsPerToken { get; init; } = 4;
    public int ChunkSize { get; init; } = 512;
    public int ChunkOverlap { get; init; } = 64;
    public decimal EmbeddingPricePerMillion { get; init; }
    public decimal ChatInputPricePerMillion { get; init; }
    public decimal ChatOutputPricePerMillion { get; init; }
    public decimal VisionInputPricePerMillion { get; init; }
    public decimal VisionOutputPricePerMillion { get; init; }
    public int VisionInputTokensPerImage { get; init; }
    public int VisionOutputTokensPerImage { get; init; }
    public int ChatOutputTokens { get; init; }
    public int RagAskContextTokens { get; init; }
    public int RagAskSystemTokens { get; init; }
    public string EmbeddingModel { get; init; } = string.Empty;
    public string ChatModel { get; init; } = string.Empty;
    public string VisionModel { get; init; } = string.Empty;
}
