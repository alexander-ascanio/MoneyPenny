using MoneyPenny.Options;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Generation;

public class OpenAiGenerationService : IGenerationService
{
    private readonly RagOptions _options;
    private readonly IWebHostEnvironment _environment;

    public OpenAiGenerationService(IOptions<RagOptions> options, IWebHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    public Task<string> GenerateAnswerAsync(string question, string context, CancellationToken cancellationToken = default)
    {
        // TODO: integrar OpenAI / Azure OpenAI con prompts de Prompts/
        _ = cancellationToken;
        var systemPromptPath = Path.Combine(_environment.ContentRootPath, _options.SystemPromptFile);
        _ = File.Exists(systemPromptPath);

        return Task.FromResult(
            "Respuesta pendiente de implementación. Conecta OpenAI en OpenAiGenerationService.");
    }
}
