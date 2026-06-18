using MoneyPenny.Models.Rag;
using MoneyPenny.Services.Rag.Generation;
using MoneyPenny.Services.Rag.Retrieval;
using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Services.Rag;

public interface IRagOrchestrator
{
    Task<RagResponseViewModel> AskAsync(AskTicketViewModel request, string userId, CancellationToken cancellationToken = default);
}

public class RagOrchestrator : IRagOrchestrator
{
    private readonly IRetrievalService _retrievalService;
    private readonly IGenerationService _generationService;
    private readonly Data.Repositories.IVectorRepository _vectorRepository;

    public RagOrchestrator(
        IRetrievalService retrievalService,
        IGenerationService generationService,
        Data.Repositories.IVectorRepository vectorRepository)
    {
        _retrievalService = retrievalService;
        _generationService = generationService;
        _vectorRepository = vectorRepository;
    }

    public async Task<RagResponseViewModel> AskAsync(
        AskTicketViewModel request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _retrievalService.RetrieveContextAsync(
            request.Question,
            request.TicketId,
            cancellationToken);

        var context = string.Join("\n---\n", chunks.Select(c => c.Content));
        var answer = await _generationService.GenerateAnswerAsync(request.Question, context, cancellationToken);

        await _vectorRepository.SaveQueryLogAsync(new RagQueryLog
        {
            UserId = userId,
            TicketId = request.TicketId,
            Question = request.Question,
            Answer = answer,
            PromptVersion = "v1-stub"
        }, cancellationToken);

        return new RagResponseViewModel
        {
            Question = request.Question,
            Answer = answer,
            ContextSnippets = chunks.Select(c => c.Content).ToList(),
            TicketId = request.TicketId,
            TicketNumber = request.TicketNumber
        };
    }
}
