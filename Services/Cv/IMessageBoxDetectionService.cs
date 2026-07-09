namespace MoneyPenny.Services.Cv;

public interface IMessageBoxDetectionService
{
    Task<MessageBoxDetectionResult> DetectAsync(
        byte[] imageBytes,
        MessageBoxTextEngine textEngine = MessageBoxTextEngine.Tesseract,
        CancellationToken cancellationToken = default);
}

public sealed class MessageBoxDetectionResult
{
    public bool Success { get; init; }
    public bool Detected { get; init; }
    public double Confidence { get; init; }
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<MessageBoxElement> Elements { get; init; } = [];
    public string? TitleText { get; init; }
    public string? MessageText { get; init; }
    public string? ErrorMessage { get; init; }

    public static MessageBoxDetectionResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };

    public static MessageBoxDetectionResult NotFound(string summary) =>
        new() { Success = true, Detected = false, Summary = summary };
}

public sealed class MessageBoxElement
{
    public required string Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double Score { get; init; }
}
