namespace MoneyPenny.ViewModels.Shared;

public class TokenUsageEstimateViewModel
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
    public string? Title { get; init; }
    public bool Compact { get; init; }

    public static TokenUsageEstimateViewModel FromEstimate(
        Services.Rag.Pricing.TokenUsageEstimate estimate,
        string? title = null,
        bool compact = false) => new()
    {
        EmbeddingInputTokens = estimate.EmbeddingInputTokens,
        EmbeddingApiCalls = estimate.EmbeddingApiCalls,
        VisionInputTokens = estimate.VisionInputTokens,
        VisionOutputTokens = estimate.VisionOutputTokens,
        VisionApiCalls = estimate.VisionApiCalls,
        ChatInputTokens = estimate.ChatInputTokens,
        ChatOutputTokens = estimate.ChatOutputTokens,
        ChatApiCalls = estimate.ChatApiCalls,
        EstimatedCostUsd = estimate.EstimatedCostUsd,
        Lines = estimate.Lines,
        Title = title,
        Compact = compact
    };
}
