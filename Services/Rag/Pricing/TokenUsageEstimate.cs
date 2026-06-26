namespace MoneyPenny.Services.Rag.Pricing;

public class TokenUsageEstimate
{
    public int EmbeddingInputTokens { get; init; }
    public int EmbeddingApiCalls { get; init; }
    public int VisionInputTokens { get; init; }
    public int VisionOutputTokens { get; init; }
    public int VisionApiCalls { get; init; }
    public int ChatInputTokens { get; init; }
    public int ChatOutputTokens { get; init; }
    public int ChatApiCalls { get; init; }
    public decimal EstimatedCostUsd { get; init; }
    public IReadOnlyList<string> Lines { get; init; } = [];
}
