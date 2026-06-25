using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Ingestion;

public class OpenAiImageTextExtractionService : IImageTextExtractionService
{
    public const string ImageDownloadHttpClientName = "ImageDownload";

    private const string ExtractionPrompt =
        "Extrae todo el texto legible de esta captura de pantalla de un sistema informático. " +
        "Incluye títulos de ventanas, mensajes de error, etiquetas de campos y botones. " +
        "Responde únicamente con el texto extraído, sin explicaciones ni comentarios.";

    private const int MaxImageBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions VisionJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RagOptions _options;
    private readonly TeamSupportApiOptions _teamSupportOptions;
    private readonly ILogger<OpenAiImageTextExtractionService> _logger;

    public OpenAiImageTextExtractionService(
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> options,
        IOptions<TeamSupportApiOptions> teamSupportOptions,
        ILogger<OpenAiImageTextExtractionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _teamSupportOptions = teamSupportOptions.Value;
        _logger = logger;
    }

    public async Task<ImageTextExtractionResult> ExtractAsync(
        IReadOnlyList<string> imageSources,
        CancellationToken cancellationToken = default)
    {
        if (imageSources.Count == 0)
        {
            return new ImageTextExtractionResult();
        }

        if (imageSources.Any(IsTeamSupportAttachmentSource)
            && string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentCookie)
            && string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentBearerToken))
        {
            return new ImageTextExtractionResult
            {
                Warning =
                    "Falta ExternalApis:TeamSupport:AttachmentCookie en appsettings.Development.json."
            };
        }

        var results = new List<string>();
        var warnings = new List<string>();
        var sources = imageSources.Take(_options.MaxImagesPerComment).ToList();

        foreach (var source in sources)
        {
            try
            {
                var dataUrl = await ResolveToDataUrlAsync(source, cancellationToken);
                if (dataUrl is null)
                {
                    warnings.Add($"No se pudo descargar la imagen: {TruncateForLog(source)}");
                    continue;
                }

                var text = await CallVisionApiAsync(dataUrl, cancellationToken);
                if (string.IsNullOrWhiteSpace(text))
                {
                    warnings.Add(
                        $"OpenAI Vision no devolvió texto para: {TruncateForLog(source)}. " +
                        "Revisa los logs de la aplicación para el detalle de la respuesta.");
                    continue;
                }

                results.Add(text.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo extraer texto de la imagen {ImageSource}.",
                    TruncateForLog(source));
                warnings.Add($"Error al procesar imagen: {ex.Message}");
            }
        }

        return new ImageTextExtractionResult
        {
            Texts = results,
            Warning = warnings.Count == 0 ? null : string.Join(" ", warnings)
        };
    }

    private async Task<string?> ResolveToDataUrlAsync(string source, CancellationToken cancellationToken)
    {
        source = Helpers.TicketHtmlHelper.SanitizeImageSource(source);
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogWarning("Origen de imagen no soportado: {ImageSource}", TruncateForLog(source));
            return null;
        }

        var downloadClient = _httpClientFactory.CreateClient(ImageDownloadHttpClientName);
        using var response = await SendDownloadRequestAsync(downloadClient, uri, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "No se pudo descargar la imagen {ImageSource}. HTTP {StatusCode}.",
                TruncateForLog(source),
                response is null ? 0 : (int)response.StatusCode);
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length == 0)
        {
            return null;
        }

        if (bytes.Length > MaxImageBytes)
        {
            _logger.LogWarning(
                "Imagen demasiado grande ({SizeBytes} bytes): {ImageSource}",
                bytes.Length,
                TruncateForLog(source));
            return null;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(contentType)
            || contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            if (LooksLikeHtml(bytes))
            {
                _logger.LogWarning(
                    "La descarga devolvió HTML en lugar de imagen (posible sesión caducada): {ImageSource}",
                    TruncateForLog(source));
                return null;
            }

            contentType = GuessImageContentType(bytes) ?? "image/png";
        }
        else if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            contentType = GuessImageContentType(bytes) ?? "image/png";
        }

        if (GuessImageContentType(bytes) is null && LooksLikeHtml(bytes))
        {
            _logger.LogWarning(
                "La descarga no parece una imagen válida: {ImageSource}",
                TruncateForLog(source));
            return null;
        }

        return $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
    }

    private async Task<string?> CallVisionApiAsync(string dataUrl, CancellationToken cancellationToken)
    {
        var text = await CallVisionApiWithModelAsync(dataUrl, _options.VisionModel, cancellationToken);
        if (!string.IsNullOrWhiteSpace(text) && !IsVisionRefusal(text))
        {
            return text;
        }

        if (string.Equals(_options.VisionModel, _options.VisionFallbackModel, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        _logger.LogInformation(
            "Reintentando extracción de imagen con modelo {FallbackModel}.",
            _options.VisionFallbackModel);

        var fallbackText = await CallVisionApiWithModelAsync(
            dataUrl,
            _options.VisionFallbackModel,
            cancellationToken);

        return IsVisionRefusal(fallbackText) ? null : fallbackText;
    }

    private async Task<string?> CallVisionApiWithModelAsync(
        string dataUrl,
        string model,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(OpenAiEmbeddingService.HttpClientName);
        var payload = new OpenAiVisionRequest
        {
            Model = model,
            MaxTokens = 700,
            Messages =
            [
                new OpenAiVisionMessage
                {
                    Role = "user",
                    Content =
                    [
                        new OpenAiVisionContentPart { Type = "text", Text = ExtractionPrompt },
                        new OpenAiVisionContentPart
                        {
                            Type = "image_url",
                            ImageUrl = new OpenAiVisionImageUrl { Url = dataUrl, Detail = "auto" }
                        }
                    ]
                }
            ]
        };

        using var response = await client.PostAsJsonAsync(
            "chat/completions",
            payload,
            VisionJsonOptions,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "OpenAI vision ({Model}) respondió {StatusCode}: {Error}",
                model,
                (int)response.StatusCode,
                responseBody);
            return null;
        }

        var text = ParseVisionResponseContent(responseBody);
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning(
                "OpenAI vision ({Model}) devolvió respuesta vacía. Body: {ResponseBody}",
                model,
                TruncateForLog(responseBody));
        }

        return text;
    }

    private static bool IsVisionRefusal(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && text.Contains("no puedo ayudar", StringComparison.OrdinalIgnoreCase);

    private static string? ParseVisionResponseContent(string responseBody)
    {
        using var document = System.Text.Json.JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != System.Text.Json.JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return null;
        }

        var choice = choices[0];
        if (choice.TryGetProperty("finish_reason", out var finishReason))
        {
            var reason = finishReason.GetString();
            if (string.Equals(reason, "content_filter", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (!choice.TryGetProperty("message", out var message))
        {
            return null;
        }

        if (message.TryGetProperty("refusal", out var refusal)
            && refusal.ValueKind == System.Text.Json.JsonValueKind.String
            && !string.IsNullOrWhiteSpace(refusal.GetString()))
        {
            return null;
        }

        if (!message.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return content.GetString()?.Trim();
        }

        if (content.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && string.Equals(type.GetString(), "text", StringComparison.OrdinalIgnoreCase)
                    && part.TryGetProperty("text", out var textPart))
                {
                    builder.AppendLine(textPart.GetString());
                }
            }

            return builder.ToString().Trim();
        }

        return null;
    }

    private async Task<HttpResponseMessage?> SendDownloadRequestAsync(
        HttpClient downloadClient,
        Uri uri,
        CancellationToken cancellationToken)
    {
        var currentUri = uri;

        for (var redirectCount = 0; redirectCount < 5; redirectCount++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            ApplyTeamSupportAuth(request, currentUri);

            var response = await downloadClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            request.Dispose();

            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                response.Dispose();

                if (location is null)
                {
                    return null;
                }

                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                _logger.LogDebug(
                    "Siguiendo redirección de adjunto TeamSupport a {RedirectUri}.",
                    currentUri);

                continue;
            }

            return response;
        }

        return null;
    }

    private void ApplyTeamSupportAuth(HttpRequestMessage request, Uri uri)
    {
        if (!IsTeamSupportAttachmentUri(uri))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentBearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                _teamSupportOptions.AttachmentBearerToken);
        }

        if (!string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentCookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _teamSupportOptions.AttachmentCookie);
        }

        if (!string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentReferer))
        {
            request.Headers.TryAddWithoutValidation("Referer", _teamSupportOptions.AttachmentReferer);
        }
    }

    private bool IsTeamSupportAttachmentSource(string source) =>
        Uri.TryCreate(Helpers.TicketHtmlHelper.SanitizeImageSource(source), UriKind.Absolute, out var uri)
        && IsTeamSupportAttachmentUri(uri);

    private bool IsTeamSupportAttachmentUri(Uri uri) =>
        _teamSupportOptions.AttachmentHosts.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeHtml(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return false;
        }

        var prefix = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 32))
            .TrimStart();
        return prefix.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GuessImageContentType(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47)
        {
            return "image/png";
        }

        if (bytes.Length >= 3
            && bytes[0] == 0xFF
            && bytes[1] == 0xD8
            && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 6
            && bytes[0] == 0x47
            && bytes[1] == 0x49
            && bytes[2] == 0x46)
        {
            return "image/gif";
        }

        return null;
    }

    private static string TruncateForLog(string value) =>
        value.Length <= 120 ? value : value[..120] + "...";

    private sealed class OpenAiVisionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAiVisionMessage> Messages { get; set; } = [];

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private sealed class OpenAiVisionMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public List<OpenAiVisionContentPart> Content { get; set; } = [];
    }

    private sealed class OpenAiVisionContentPart
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonPropertyName("image_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OpenAiVisionImageUrl? ImageUrl { get; set; }
    }

    private sealed class OpenAiVisionImageUrl
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "high";
    }

    private sealed class OpenAiVisionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiVisionChoice>? Choices { get; set; }
    }

    private sealed class OpenAiVisionChoice
    {
        [JsonPropertyName("message")]
        public OpenAiVisionChoiceMessage? Message { get; set; }
    }

    private sealed class OpenAiVisionChoiceMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
