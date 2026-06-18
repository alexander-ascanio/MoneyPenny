namespace MoneyPenny.Services.Rag.Generation;

public interface IGenerationService
{
    Task<string> GenerateAnswerAsync(string question, string context, CancellationToken cancellationToken = default);
}
