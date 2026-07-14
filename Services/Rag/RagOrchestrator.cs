using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Rag;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Generation;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Retrieval;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.ViewModels.Rag;
using MoneyPenny.ViewModels.Tickets;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag;

public interface IRagOrchestrator
{
    Task<RagResponseViewModel> ProcessTicketAsync(
        AskTicketViewModel request,
        string userId,
        CancellationToken cancellationToken = default);

    Task<RagThresholdComparisonViewModel> CompareThresholdsAsync(
        int ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly,
        CancellationToken cancellationToken = default);
}

public class RagOrchestrator : IRagOrchestrator
{
    public const string DefaultGenerationQuestion =
        "Redacta un mensaje de respuesta para el cliente que resuelva o oriente sobre el problema descrito en su comentario inicial.";

    public const string KnowledgeBasePromptVersion = "kb-solution-v1";

    private readonly IRetrievalService _retrievalService;
    private readonly IGenerationService _generationService;
    private readonly IVectorRepository _vectorRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ICommentContentService _commentContentService;
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly ITeamSupportActionApiClient _teamSupportActionApiClient;
    private readonly RagOptions _options;

    public RagOrchestrator(
        IRetrievalService retrievalService,
        IGenerationService generationService,
        IVectorRepository vectorRepository,
        ITicketRepository ticketRepository,
        ICommentContentService commentContentService,
        ITeamSupportAttachmentService attachmentService,
        ITeamSupportActionApiClient teamSupportActionApiClient,
        IOptions<RagOptions> options)
    {
        _retrievalService = retrievalService;
        _generationService = generationService;
        _vectorRepository = vectorRepository;
        _ticketRepository = ticketRepository;
        _commentContentService = commentContentService;
        _attachmentService = attachmentService;
        _teamSupportActionApiClient = teamSupportActionApiClient;
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
            request.MinScoreOverride,
            cancellationToken: cancellationToken);

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

        var knowledgeBaseLog = request.SkipQueryLog
            ? null
            : await TrySaveKnowledgeBaseQueryLogAsync(
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

        var gptLog = request.SkipQueryLog
            ? null
            : await _vectorRepository.SaveQueryLogAsync(new RagQueryLog
            {
                UserId = userId,
                TicketId = request.TicketId,
                Question = $"{DefaultGenerationQuestion} (ticket #{request.TicketNumber})",
                Answer = answer,
                PromptVersion = OpenAiGenerationService.PromptVersion,
                ResponseType = RagResponseType.Gpt
            }, cancellationToken: cancellationToken);

        var actionInsertResult = request.SkipTeamSupportActionInsert
            ? new TeamSupportActionCreateResult()
            : await TryInsertPrivateGptActionAsync(
                firstComment,
                request,
                answer,
                cancellationToken);

        if (!request.SkipTeamSupportActionInsert
            && gptLog is not null
            && actionInsertResult.Success
            && !string.IsNullOrWhiteSpace(actionInsertResult.ActionId))
        {
            await _vectorRepository.UpdateQueryLogTeamSupportActionIdAsync(
                gptLog.Id,
                actionInsertResult.ActionId,
                cancellationToken);
        }

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
            GptQueryLogId = gptLog?.Id,
            GptRating = gptLog?.Rating,
            KnowledgeBaseQueryLogId = knowledgeBaseLog?.Id,
            KnowledgeBaseRating = knowledgeBaseLog?.Rating,
            GptTeamSupportActionInserted = !request.SkipTeamSupportActionInsert && actionInsertResult.Success,
            GptTeamSupportActionId = request.SkipTeamSupportActionInsert ? null : actionInsertResult.ActionId,
            GptTeamSupportActionWarning = request.SkipTeamSupportActionInsert || actionInsertResult.Success
                ? null
                : actionInsertResult.ErrorMessage
        };
    }

    private async Task<TeamSupportActionCreateResult> TryInsertPrivateGptActionAsync(
        RagFirstCommentViewModel firstComment,
        AskTicketViewModel request,
        string answer,
        CancellationToken cancellationToken)
    {
        var teamSupportTicketId = ResolveTeamSupportTicketId(firstComment, request);
        var commentHtml = TeamSupportCommentHtmlHelper.ToPrivateCommentHtml(answer);
        if (string.IsNullOrWhiteSpace(commentHtml))
        {
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "La respuesta GPT está vacía; no se insertó comentario en TeamSupport."
            };
        }

        return await _teamSupportActionApiClient.CreatePrivateCommentAsync(
            teamSupportTicketId,
            commentHtml,
            cancellationToken: cancellationToken);
    }

    private static string ResolveTeamSupportTicketId(
        RagFirstCommentViewModel firstComment,
        AskTicketViewModel request)
    {
        if (!string.IsNullOrWhiteSpace(firstComment.TeamSupportTicketId))
        {
            return firstComment.TeamSupportTicketId;
        }

        if (!string.IsNullOrWhiteSpace(request.TicketNumber))
        {
            return request.TicketNumber;
        }

        return request.TicketId.ToString();
    }

    public async Task<RagThresholdComparisonViewModel> CompareThresholdsAsync(
        int ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly,
        CancellationToken cancellationToken = default)
    {
        var firstComment = await LoadFirstCommentAsync(ticketId, cancellationToken);
        if (firstComment is null || string.IsNullOrWhiteSpace(firstComment.Content))
        {
            return new RagThresholdComparisonViewModel
            {
                TicketId = ticketId,
                TicketNumber = ticketNumber,
                KnowledgeBaseOnly = knowledgeBaseOnly,
                ErrorMessage = "Este ticket no tiene un comentario #1 con contenido indexable.",
                ThresholdValues = ResolveCompareThresholds()
            };
        }

        var thresholds = ResolveCompareThresholds();
        var queryVector = await _retrievalService.CreateQueryEmbeddingAsync(
            firstComment.Content,
            cancellationToken);

        var columns = new List<RagThresholdComparisonColumnViewModel>();
        foreach (var minScore in thresholds)
        {
            var column = await BuildThresholdColumnAsync(
                firstComment,
                ticketId,
                ticketNumber,
                knowledgeBaseOnly,
                minScore,
                queryVector,
                cancellationToken);
            columns.Add(column);
        }

        return new RagThresholdComparisonViewModel
        {
            TicketId = ticketId,
            TicketNumber = ticketNumber,
            KnowledgeBaseOnly = knowledgeBaseOnly,
            FirstComment = firstComment,
            HasComparison = true,
            ThresholdValues = thresholds,
            Columns = columns
        };
    }

    private IReadOnlyList<double> ResolveCompareThresholds()
    {
        var values = _options.CompareThresholdValues is { Length: > 0 }
            ? _options.CompareThresholdValues
            : [_options.MinScore, 0.55, 0.45];

        return values
            .Where(value => value is >= 0 and <= 1)
            .Distinct()
            .OrderByDescending(value => value)
            .Take(3)
            .ToList();
    }

    private async Task<RagThresholdComparisonColumnViewModel> BuildThresholdColumnAsync(
        RagFirstCommentViewModel firstComment,
        int ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly,
        double minScore,
        float[] queryVector,
        CancellationToken cancellationToken)
    {
        var retrieved = await _retrievalService.RetrieveSimilarFirstCommentsAsync(
            firstComment.Content,
            ticketId,
            knowledgeBaseOnly,
            minScore,
            queryVector,
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

        if (contextItems.Count == 0)
        {
            return new RagThresholdComparisonColumnViewModel
            {
                MinScore = minScore,
                ContextItems = contextItems,
                ErrorMessage = $"Ningún ticket supera el umbral {minScore:P0}."
            };
        }

        var gptContextText = await BuildGptContextTextAsync(
            knowledgeBaseOnly,
            contextItems,
            cancellationToken);

        var answer = await _generationService.GenerateAnswerAsync(
            DefaultGenerationQuestion,
            gptContextText,
            ticketNumber,
            firstComment.Content,
            cancellationToken);

        return new RagThresholdComparisonColumnViewModel
        {
            MinScore = minScore,
            ContextItems = contextItems,
            Answer = answer,
            HasGptAnswer = !string.IsNullOrWhiteSpace(answer)
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

        var (ticket, attachmentViewModels, pendingAttachmentResolution) =
            await LoadFirstCommentAttachmentsAsync(action, cancellationToken);

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
                    TicketActionId = action.Id,
                    TeamSupportActionId = action.TeamSupportActionId,
                    TeamSupportTicketId = ticket?.TeamSupportId
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
            ImageExtractionWarning = warning,
            TeamSupportActionId = action.TeamSupportActionId,
            TeamSupportTicketId = ticket?.TeamSupportId,
            Attachments = attachmentViewModels,
            PendingAttachmentResolution = pendingAttachmentResolution
        };
    }

    private async Task<(Ticket? Ticket, IReadOnlyList<TicketAttachmentViewModel> Attachments, bool PendingAttachmentResolution)>
        LoadFirstCommentAttachmentsAsync(
            TicketAction action,
            CancellationToken cancellationToken)
    {
        var ticket = await _ticketRepository.GetByIdAsync(action.TicketId, cancellationToken);
        var resolved = await _attachmentService.ResolveAttachmentsAsync(
            action.TeamSupportActionId,
            ticket?.TeamSupportId,
            action.Content,
            cancellationToken);

        var attachmentViewModels = resolved
            .Select(item => new TicketAttachmentViewModel
            {
                OriginalUrl = item.OriginalUrl,
                FileName = item.FileName,
                IsImage = item.IsImage
            })
            .ToList();

        var pendingAttachmentResolution = attachmentViewModels.Count == 0
            && !string.IsNullOrWhiteSpace(action.TeamSupportActionId)
            && TicketHtmlHelper.ContentMentionsAttachment(action.Content);

        return (ticket, attachmentViewModels, pendingAttachmentResolution);
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
