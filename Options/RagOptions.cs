namespace MoneyPenny.Options;

public class RagOptions
{
    public const string SectionName = "Rag";

    public int ChunkSize { get; set; } = 512;
    public int ChunkOverlap { get; set; } = 64;
    public int TopK { get; set; } = 5;
    public double MinScore { get; set; } = 0.72;
    public double TicketScopedMinScore { get; set; } = 0.55;
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";
    public int EmbeddingDimensions { get; set; } = 1536;
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string VisionModel { get; set; } = "gpt-4o-mini";
    public string VisionFallbackModel { get; set; } = "gpt-4o-mini";
    public bool EnableImageTextExtraction { get; set; } = true;
    public int MaxImagesPerComment { get; set; } = 3;
    public string SystemPromptFile { get; set; } = "Prompts/system.txt";
    public string TicketQaPromptFile { get; set; } = "Prompts/ticket-qa.txt";
}
