namespace MoneyPenny.Services.Ocr;

public interface ICommentImageOcrService
{
    Task<CommentImageOcrResult> ExtractTextFromUrlAsync(string url, CancellationToken cancellationToken = default);
}

public sealed class CommentImageOcrResult
{
    public bool Success { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }

    public static CommentImageOcrResult Ok(string text) =>
        new() { Success = true, Text = text };

    public static CommentImageOcrResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
