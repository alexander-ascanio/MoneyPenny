namespace MoneyPenny.Services.Rag.Pricing;

public interface IRagTokenEstimateService
{
    int EstimateTokensFromText(string? text);
    int EstimateChunkCount(int textLength);
    TokenUsageEstimate EstimateEmbeddingsForTexts(IReadOnlyList<string> texts);
    TokenUsageEstimate EstimateTicketIndex(
        string documentText,
        int imageCount,
        bool processImages);
    TokenUsageEstimate EstimateFirstCommentBulkIndex(
        int ticketsToProcess,
        int averageCommentCharCount,
        bool processImages,
        double averageImagesPerTicket = 0);
    TokenUsageEstimate EstimateRagContextLoad(string? firstCommentText);
    TokenUsageEstimate EstimateRagGptAnswer(string? contextText);
    TokenUsageEstimate Combine(params TokenUsageEstimate[] estimates);
}
