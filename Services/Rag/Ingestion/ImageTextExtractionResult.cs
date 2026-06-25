namespace MoneyPenny.Services.Rag.Ingestion;

public sealed class ImageTextExtractionResult
{
    public IReadOnlyList<string> Texts { get; init; } = [];
    public string? Warning { get; init; }
}
