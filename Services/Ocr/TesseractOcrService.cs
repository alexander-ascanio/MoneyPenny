using Microsoft.Extensions.Options;
using MoneyPenny.Options;
using Tesseract;

namespace MoneyPenny.Services.Ocr;

public sealed class TesseractOcrService : ITesseractOcrService, IDisposable
{
    private readonly TesseractOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TesseractOcrService> _logger;
    private readonly SemaphoreSlim _engineLock = new(1, 1);
    private TesseractEngine? _engine;
    private bool _disposed;

    public TesseractOcrService(
        IOptions<TesseractOptions> options,
        IWebHostEnvironment environment,
        ILogger<TesseractOcrService> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> ExtractTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (imageBytes.Length == 0)
        {
            return string.Empty;
        }

        await _engineLock.WaitAsync(cancellationToken);
        try
        {
            var engine = GetOrCreateEngine();
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);
            return page.GetText()?.Trim() ?? string.Empty;
        }
        finally
        {
            _engineLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine?.Dispose();
        _engineLock.Dispose();
    }

    private TesseractEngine GetOrCreateEngine()
    {
        if (_engine is not null)
        {
            return _engine;
        }

        var tessDataPath = ResolveTessDataPath();
        if (!Directory.Exists(tessDataPath))
        {
            throw new DirectoryNotFoundException(
                $"No se encontró la carpeta tessdata en '{tessDataPath}'. Descarga eng.traineddata y spa.traineddata.");
        }

        _logger.LogInformation(
            "Inicializando Tesseract OCR (idiomas: {Languages}, tessdata: {TessDataPath}).",
            _options.Languages,
            tessDataPath);

        _engine = new TesseractEngine(tessDataPath, _options.Languages, EngineMode.Default);
        return _engine;
    }

    private string ResolveTessDataPath()
    {
        var configuredPath = _options.TessDataPath?.Trim();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.Combine(_environment.ContentRootPath, "tessdata");
        }

        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(_environment.ContentRootPath, configuredPath);
    }
}
