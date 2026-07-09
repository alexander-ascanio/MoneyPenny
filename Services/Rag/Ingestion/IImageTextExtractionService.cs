namespace MoneyPenny.Services.Rag.Ingestion;

public interface IImageTextExtractionService
{
    Task<ImageTextExtractionResult> ExtractAsync(
        IReadOnlyList<string> imageSources,
        CancellationToken cancellationToken = default);

    Task<string?> ExtractFromBytesAsync(
        byte[] imageBytes,
        string? prompt = null,
        CancellationToken cancellationToken = default);
}
