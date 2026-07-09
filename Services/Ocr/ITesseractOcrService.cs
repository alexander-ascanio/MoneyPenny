namespace MoneyPenny.Services.Ocr;

public interface ITesseractOcrService
{
    Task<string> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default);
}
