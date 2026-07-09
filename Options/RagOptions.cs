namespace MoneyPenny.Options;

public class RagOptions
{
    public const string SectionName = "Rag";

    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 64;
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.72;
    public double TicketScopedMinScore { get; set; } = 0.55;
    public double[] CompareThresholdValues { get; set; } = [0.72, 0.55, 0.45];
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 1536;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string VisionModel { get; set; } = "gpt-4o-mini";
    public string VisionFallbackModel { get; set; } = "gpt-4o-mini";
    public bool EnableImageTextExtraction { get; set; } = true;
    public int MaxImagesPerComment { get; set; } = 3;
    public string SystemPromptFile { get; set; } = "Prompts/system.txt";
    public string TicketQaPromptFile { get; set; } = "Prompts/ticket-qa.txt";
    public int FirstCommentIndexBatchSize { get; set; } = 25;

    public int CharsPerTokenEstimate { get; set; } = 4;
    public decimal EmbeddingPricePerMillionTokens { get; set; } = 0.02m;
    public decimal ChatInputPricePerMillionTokens { get; set; } = 0.15m;
    public decimal ChatOutputPricePerMillionTokens { get; set; } = 0.60m;
    public decimal VisionInputPricePerMillionTokens { get; set; } = 0.15m;
    public decimal VisionOutputPricePerMillionTokens { get; set; } = 0.60m;
    public int VisionEstimatedInputTokensPerImage { get; set; } = 1200;
    public int VisionEstimatedOutputTokensPerImage { get; set; } = 200;
    public int ChatEstimatedOutputTokens { get; set; } = 400;
    public int RagAskEstimatedContextTokens { get; set; } = 2500;
    public int RagAskEstimatedSystemTokens { get; set; } = 150;
    public RagGroundingCheckOptions GroundingCheck { get; set; } = new();
}
