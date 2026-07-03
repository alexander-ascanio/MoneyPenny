using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Generation;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Retrieval;
using MoneyPenny.ViewModels.Rag;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag;

public interface IRagOrchestrator
{
    Task<RagResponseViewModel> ProcessTicketAsync(
        AskTicketViewModel request,
        string userId,
        CancellationToken cancellationToken = default);
}

public class RagOrchestrator : IRagOrchestrator
{
    public const string DefaultGenerationQuestion =
        "Propón una solución o pasos de resolución para el problema descrito en el comentario del cliente.";

    public const string KnowledgeBasePromptVersion = "kb-solution-v1";

    private readonly IRetrievalService _retrievalService;
    private readonly IGenerationService _generationService;
    private readonly IVectorRepository _vectorRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ICommentContentService _commentContentService;
    private readonly RagOptions _options;

    public RagOrchestrator(
        IRetrievalService retrievalService,
        IGenerationService generationService,
        IVectorRepository vectorRepository,
        ITicketRepository ticketRepository,
        ICommentContentService commentContentService,
        IOptions<RagOptions> options)
    {
        _retrievalService = retrievalService;
        _generationService = generationService;
        _vectorRepository = vectorRepository;
        _ticketRepository = ticketRepository;
        _commentContentService = commentContentService;
        _options = options.Value;
    }

    public async Task<RagResponseViewModel> ProcessTicketAsync(
        AskTicketViewModel request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var firstComment = await LoadFirstCommentAsync(request.TicketId, cancellationToken);
        if (firstComment is null || string.IsNullOrWhiteSpace(firstComment.Content))
        {
            return new RagResponseViewModel
            {
                TicketId = request.TicketId,
                TicketNumber = request.TicketNumber,
                KnowledgeBaseOnly = request.KnowledgeBaseOnly,
                ErrorMessage = "Este ticket no tiene un comentario #1 con contenido indexable."
            };
        }

        var retrieved = await _retrievalService.RetrieveSimilarFirstCommentsAsync(
            firstComment.Content,
            request.TicketId,
            request.KnowledgeBaseOnly,
            cancellationToken);

        var contextItems = new List<RagContextItemViewModel>();
        foreach (var item in retrieved)
        {
            var content = await GetRetrievedContextContentAsync(
                item.Chunk.TicketId,
                item.Chunk.Content,
                cancellationToken);

            contextItems.Add(new RagContextItemViewModel
            {
                TicketId = item.Chunk.TicketId,
                TicketNumber = item.Chunk.TicketNumber,
                ChunkIndex = item.Chunk.ChunkIndex,
                Score = item.Score,
                Content = content
            });
        }

        var knowledgeBaseSolution = TryBuildKnowledgeBaseSolution(request.KnowledgeBaseOnly, contextItems);
        var gptContextText = await BuildGptContextTextAsync(
            request.KnowledgeBaseOnly,
            contextItems,
            cancellationToken);

        var knowledgeBaseLog = await TrySaveKnowledgeBaseQueryLogAsync(
            request,
            userId,
            knowledgeBaseSolution,
            cancellationToken);

        if (!request.GenerateGptAnswer)
        {
            return new RagResponseViewModel
            {
                ContextItems = contextItems,
                FirstComment = firstComment,
                TicketId = request.TicketId,
                TicketNumber = request.TicketNumber,
                KnowledgeBaseOnly = request.KnowledgeBaseOnly,
                KnowledgeBaseSolution = knowledgeBaseSolution,
                GptContextText = gptContextText,
                KnowledgeBaseQueryLogId = knowledgeBaseLog?.Id,
                KnowledgeBaseRating = knowledgeBaseLog?.Rating
            };
        }

        var answer = await _generationService.GenerateAnswerAsync(
            DefaultGenerationQuestion,
            gptContextText,
            request.TicketNumber,
            firstComment.Content,
            cancellationToken);

        var gptLog = await _vectorRepository.SaveQueryLogAsync(new RagQueryLog
        {
            UserId = userId,
            TicketId = request.TicketId,
            Question = $"{DefaultGenerationQuestion} (ticket #{request.TicketNumber})",
            Answer = answer,
            PromptVersion = OpenAiGenerationService.PromptVersion,
            ResponseType = RagResponseType.Gpt
        }, cancellationToken: cancellationToken);

        return new RagResponseViewModel
        {
            Answer = answer,
            HasGptAnswer = true,
            ContextItems = contextItems,
            FirstComment = firstComment,
            TicketId = request.TicketId,
            TicketNumber = request.TicketNumber,
            KnowledgeBaseOnly = request.KnowledgeBaseOnly,
            KnowledgeBaseSolution = knowledgeBaseSolution,
            GptContextText = gptContextText,
            GptQueryLogId = gptLog.Id,
            GptRating = gptLog.Rating,
            KnowledgeBaseQueryLogId = knowledgeBaseLog?.Id,
            KnowledgeBaseRating = knowledgeBaseLog?.Rating
        };
    }

    private async Task<RagQueryLog?> TrySaveKnowledgeBaseQueryLogAsync(
        AskTicketViewModel request,
        string userId,
        RagKnowledgeBaseSolutionViewModel? knowledgeBaseSolution,
        CancellationToken cancellationToken)
    {
        if (!request.KnowledgeBaseOnly || knowledgeBaseSolution is null)
        {
            return null;
        }

        return await _vectorRepository.SaveQueryLogAsync(
            new RagQueryLog
            {
                UserId = userId,
                TicketId = request.TicketId,
                Question = $"Solución Knowledge Base extraída (ticket #{request.TicketNumber})",
                Answer = knowledgeBaseSolution.Text,
                PromptVersion = KnowledgeBasePromptVersion,
                ResponseType = RagResponseType.KnowledgeBase
            },
            reuseIfUnrated: true,
            cancellationToken);
    }

    private async Task<string> BuildGptContextTextAsync(
        bool knowledgeBaseOnly,
        IReadOnlyList<RagContextItemViewModel> contextItems,
        CancellationToken cancellationToken)
    {
        if (contextItems.Count == 0)
        {
            return string.Empty;
        }

        if (knowledgeBaseOnly)
        {
            return BuildGptContext(contextItems);
        }

        return await SimilarTicketThreadContextBuilder.BuildAsync(
            contextItems,
            _ticketRepository,
            cancellationToken);
    }

    private static RagKnowledgeBaseSolutionViewModel? TryBuildKnowledgeBaseSolution(
        bool knowledgeBaseOnly,
        IReadOnlyList<RagContextItemViewModel> contextItems)
    {
        if (!knowledgeBaseOnly || contextItems.Count == 0)
        {
            return null;
        }

        var bestMatch = contextItems[0];
        var solutionText = KnowledgeBaseSolutionExtractor.Extract(bestMatch.Content);
        if (string.IsNullOrWhiteSpace(solutionText))
        {
            return null;
        }

        return new RagKnowledgeBaseSolutionViewModel
        {
            TicketId = bestMatch.TicketId,
            TicketNumber = bestMatch.TicketNumber,
            Score = bestMatch.Score,
            Text = solutionText
        };
    }

    private static string BuildGptContext(IReadOnlyList<RagContextItemViewModel> contextItems)
    {
        if (contextItems.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            "\n---\n",
            contextItems.Select(item =>
                $"Ticket #{item.TicketNumber} (similitud {item.Score:P0}):\n{item.Content}"));
    }

    private async Task<RagFirstCommentViewModel?> LoadFirstCommentAsync(
        int ticketId,
        CancellationToken cancellationToken)
    {
        var action = await _ticketRepository.GetOldestActionWithContentByTicketIdAsync(
            ticketId,
            cancellationToken);

        if (action is null)
        {
            return null;
        }

        var indexedText = await TryGetIndexedFirstCommentTextAsync(ticketId, cancellationToken);
        string displayText;
        string? warning = null;

        if (!string.IsNullOrWhiteSpace(indexedText))
        {
            displayText = indexedText;
        }
        else
        {
            var commentContent = await _commentContentService.ToIndexableContentAsync(
                action.Content,
                new CommentContentRequest
                {
                    ProcessImages = false,
                    ImageCacheMode = ImageExtractionCacheMode.CacheOnly,
                    TicketId = ticketId,
                    TicketActionId = action.Id
                },
                cancellationToken);

            if (string.IsNullOrWhiteSpace(commentContent.Text))
            {
                return null;
            }

            displayText = commentContent.Text;
            warning = commentContent.ImageExtractionWarning;
        }

        return new RagFirstCommentViewModel
        {
            Author = action.CreatedByName
                ?? action.ModifierName
                ?? action.AssignedUsername
                ?? "Desconocido",
            CreatedAt = action.CreatedAt,
            OriginalContent = action.Content ?? string.Empty,
            Content = displayText,
            ImageExtractionWarning = warning
        };
    }

    private async Task<string?> TryGetIndexedFirstCommentTextAsync(
        int ticketId,
        CancellationToken cancellationToken)
    {
        var firstCommentChunks = await _vectorRepository.GetChunksByTicketAndSourceAsync(
            ticketId,
            DocumentChunkSource.ClientFirstComment,
            cancellationToken);

        var fromFirstCommentIndex = IndexedCommentTextHelper.ExtractFromClientFirstCommentIndex(
            firstCommentChunks.Select(c => c.Content));
        if (!string.IsNullOrWhiteSpace(fromFirstCommentIndex))
        {
            return fromFirstCommentIndex;
        }

        var ticketDocumentChunks = await _vectorRepository.GetChunksByTicketAndSourceAsync(
            ticketId,
            DocumentChunkSource.TicketDocument,
            cancellationToken);

        return IndexedCommentTextHelper.ExtractFromTicketDocumentIndex(
            ticketDocumentChunks.Select(c => c.Content));
    }

    private async Task<string> GetRetrievedContextContentAsync(
        int ticketId,
        string matchedChunkContent,
        CancellationToken cancellationToken)
    {
        var chunks = await _vectorRepository.GetChunksByTicketAndSourceAsync(
            ticketId,
            DocumentChunkSource.ClientFirstComment,
            cancellationToken);

        if (chunks.Count <= 1)
        {
            return matchedChunkContent;
        }

        return ChunkingService.ReassembleChunkContents(
            chunks,
            _options.ChunkSize,
            _options.ChunkOverlap);
    }
}
