using MoneyPenny.Data.Repositories;
using MoneyPenny.Models.Rag;
using MoneyPenny.Services.Rag.Generation;
using MoneyPenny.Services.Rag.Ingestion;
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
    private readonly IVectorRepository _vectorRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ICommentContentService _commentContentService;

    public RagOrchestrator(
        IRetrievalService retrievalService,
        IGenerationService generationService,
        IVectorRepository vectorRepository,
        ITicketRepository ticketRepository,
        ICommentContentService commentContentService)
    {
        _retrievalService = retrievalService;
        _generationService = generationService;
        _vectorRepository = vectorRepository;
        _ticketRepository = ticketRepository;
        _commentContentService = commentContentService;
    }

    public async Task<RagResponseViewModel> AskAsync(
        AskTicketViewModel request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var retrieved = await _retrievalService.RetrieveContextAsync(
            request.Question,
            request.TicketId,
            cancellationToken);

        var contextItems = retrieved.Select(item => new RagContextItemViewModel
        {
            ChunkIndex = item.Chunk.ChunkIndex,
            Score = item.Score,
            Content = item.Chunk.Content
        }).ToList();

        var context = string.Join("\n---\n", contextItems.Select(c => c.Content));
        var firstComment = await LoadFirstCommentAsync(request.TicketId, cancellationToken);

        string answer;
        if (request.PreviewContextOnly)
        {
            answer = "Modo previsualización: no se generó respuesta GPT. Revisa el contexto recuperado y compáralo con el primer comentario.";
        }
        else
        {
            answer = await _generationService.GenerateAnswerAsync(request.Question, context, cancellationToken);
        }

        await _vectorRepository.SaveQueryLogAsync(new RagQueryLog
        {
            UserId = userId,
            TicketId = request.TicketId,
            Question = request.Question,
            Answer = answer,
            PromptVersion = request.PreviewContextOnly
                ? "preview-context-only"
                : OpenAiGenerationService.PromptVersion
        }, cancellationToken);

        return new RagResponseViewModel
        {
            Question = request.Question,
            Answer = answer,
            ContextItems = contextItems,
            FirstComment = firstComment,
            TicketId = request.TicketId,
            TicketNumber = request.TicketNumber,
            PreviewContextOnly = request.PreviewContextOnly
        };
    }

    private async Task<RagFirstCommentViewModel?> LoadFirstCommentAsync(
        int? ticketId,
        CancellationToken cancellationToken)
    {
        if (ticketId is null)
        {
            return null;
        }

        var action = await _ticketRepository.GetOldestActionWithContentByTicketIdAsync(
            ticketId.Value,
            cancellationToken);

        if (action is null)
        {
            return null;
        }

        var commentContent = await _commentContentService.ToIndexableContentAsync(
            action.Content,
            processImages: true,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(commentContent.Text))
        {
            return null;
        }

        return new RagFirstCommentViewModel
        {
            Author = action.CreatedByName
                ?? action.ModifierName
                ?? action.AssignedUsername
                ?? "Desconocido",
            CreatedAt = action.CreatedAt,
            Content = commentContent.Text
        };
    }
}
