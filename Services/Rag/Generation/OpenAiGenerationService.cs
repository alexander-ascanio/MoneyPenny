using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Embeddings;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Generation;

public class OpenAiGenerationService : IGenerationService
{
    public const string PromptVersion = "v2-current-ticket-comment";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly RagOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<OpenAiGenerationService> _logger;

    public OpenAiGenerationService(
        IHttpClientFactory httpClientFactory,
        IOptions<RagOptions> options,
        IWebHostEnvironment environment,
        ILogger<OpenAiGenerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<string> GenerateAnswerAsync(
        string question,
        string context,
        string? currentTicketNumber = null,
        string? currentTicketFirstComment = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("La pregunta no puede estar vacía.", nameof(question));
        }

        if (string.IsNullOrWhiteSpace(currentTicketFirstComment))
        {
            throw new ArgumentException(
                "El comentario #1 indexado del ticket actual es obligatorio para generar la respuesta.",
                nameof(currentTicketFirstComment));
        }

        var systemPrompt = await LoadPromptFileAsync(_options.SystemPromptFile, cancellationToken);
        var userPromptTemplate = await LoadPromptFileAsync(_options.TicketQaPromptFile, cancellationToken);
        var userPrompt = userPromptTemplate
            .Replace("{{ticketNumber}}", string.IsNullOrWhiteSpace(currentTicketNumber) ? "N/D" : currentTicketNumber.Trim(), StringComparison.Ordinal)
            .Replace("{{currentTicketComment}}", currentTicketFirstComment.Trim(), StringComparison.Ordinal)
            .Replace("{{context}}", string.IsNullOrWhiteSpace(context) ? "(Sin tickets similares recuperados)" : context, StringComparison.Ordinal)
            .Replace("{{question}}", question.Trim(), StringComparison.Ordinal);

        var payload = new OpenAiChatRequest
        {
            Model = _options.ChatModel,
            Messages =
            [
                new OpenAiChatMessage { Role = "system", Content = systemPrompt },
                new OpenAiChatMessage { Role = "user", Content = userPrompt }
            ],
            Temperature = 0.2
        };

        var client = _httpClientFactory.CreateClient(OpenAiEmbeddingService.HttpClientName);
        using var response = await client.PostAsJsonAsync("chat/completions", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OpenAI chat respondió {StatusCode}: {Error}",
                (int)response.StatusCode,
                errorBody);

            throw new InvalidOperationException(
                $"No se pudo obtener la respuesta de OpenAI ({(int)response.StatusCode}). " +
                $"Verifica ExternalApis:OpenAI:ApiKey y el modelo '{_options.ChatModel}'. Detalle: {errorBody}");
        }

        var result = await response.Content.ReadFromJsonAsync<OpenAiChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("OpenAI devolvió una respuesta vacía.");

        var answer = result.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
        if (string.IsNullOrWhiteSpace(answer))
        {
            throw new InvalidOperationException("OpenAI no devolvió contenido en la respuesta.");
        }

        _logger.LogInformation(
            "Respuesta generada con modelo {Model} ({Length} caracteres).",
            _options.ChatModel,
            answer.Length);

        return answer;
    }

    private async Task<string> LoadPromptFileAsync(string relativePath, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_environment.ContentRootPath, relativePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No se encontró el archivo de prompt: {fullPath}");
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }

    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAiChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChatChoice
    {
        [JsonPropertyName("message")]
        public OpenAiChatMessage? Message { get; set; }
    }
}
