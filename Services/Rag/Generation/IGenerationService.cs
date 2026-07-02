namespace MoneyPenny.Services.Rag.Generation;

public interface IGenerationService
{
    Task<string> GenerateAnswerAsync(
        string question,
        string context,
        string? currentTicketNumber = null,
        string? currentTicketFirstComment = null,
        CancellationToken cancellationToken = default);
}
