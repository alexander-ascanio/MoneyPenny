using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Validation;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.ViewModels.Rag;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag;

public class TicketRagProcessService : ITicketRagProcessService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly ITicketIngestionService _ingestionService;
    private readonly IRagOrchestrator _ragOrchestrator;
    private readonly IResponseGroundingChecker _groundingChecker;
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly RagOptions _ragOptions;

    public TicketRagProcessService(
        ITicketRepository ticketRepository,
        ITicketIngestionService ingestionService,
        IRagOrchestrator ragOrchestrator,
        IResponseGroundingChecker groundingChecker,
        ITeamSupportAttachmentService attachmentService,
        IOptions<RagOptions> ragOptions)
    {
        _ticketRepository = ticketRepository;
        _ingestionService = ingestionService;
        _ragOrchestrator = ragOrchestrator;
        _groundingChecker = groundingChecker;
        _attachmentService = attachmentService;
        _ragOptions = ragOptions.Value;
    }

    public async Task<TicketRagProcessResultViewModel> ProcessTicketAsync(
        int? ticketId,
        string? ticketNumber,
        string userId,
        bool processImages = true,
        CancellationToken cancellationToken = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ticketNumber, cancellationToken);
        if (ticket is null)
        {
            return new TicketRagProcessResultViewModel
            {
                Success = false,
                ErrorMessage = "Ticket no encontrado. Indica ticketId o ticketNumber."
            };
        }

        try
        {
            var effectiveProcessImages = await ResolveProcessImagesAsync(
                ticket,
                processImages,
                cancellationToken);

            var indexResult = await _ingestionService.IndexTicketAsync(
                ticket.Id,
                effectiveProcessImages,
                cancellationToken);

            var ragResponse = await _ragOrchestrator.ProcessTicketAsync(
                new AskTicketViewModel
                {
                    TicketId = ticket.Id,
                    TicketNumber = ticket.Number,
                    GenerateGptAnswer = true,
                    KnowledgeBaseOnly = false,
                    SkipTeamSupportActionInsert = true
                },
                userId,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(ragResponse.ErrorMessage))
            {
                return new TicketRagProcessResultViewModel
                {
                    Success = false,
                    ErrorMessage = ragResponse.ErrorMessage,
                    Ticket = MapTicket(ticket),
                    Indexing = MapIndexing(indexResult)
                };
            }

            ResponseGroundingReportViewModel? grounding = null;
            if (ragResponse.HasGptAnswer && !string.IsNullOrWhiteSpace(ragResponse.Answer))
            {
                grounding = _groundingChecker.Evaluate(new ResponseGroundingRequest
                {
                    Answer = ragResponse.Answer,
                    FirstCommentContent = ragResponse.FirstComment?.Content,
                    ContextItems = ragResponse.ContextItems,
                    TicketNumber = ragResponse.TicketNumber,
                    KnowledgeBaseSolutionText = ragResponse.KnowledgeBaseSolution?.Text
                });
            }

            return new TicketRagProcessResultViewModel
            {
                Success = ragResponse.HasGptAnswer,
                ErrorMessage = ragResponse.HasGptAnswer
                    ? null
                    : "No se pudo generar la respuesta GPT para este ticket.",
                Ticket = MapTicket(ticket),
                Indexing = MapIndexing(indexResult),
                Gpt = new TicketRagProcessGptViewModel
                {
                    HasAnswer = ragResponse.HasGptAnswer,
                    Answer = ragResponse.Answer,
                    QueryLogId = ragResponse.GptQueryLogId,
                    ContextTicketCount = ragResponse.ContextItems.Count
                },
                GroundingCheck = grounding is null
                    ? null
                    : GroundingCheckHtmlHelper.EnrichWithCommentHtml(grounding)
            };
        }
        catch (Exception ex)
        {
            return new TicketRagProcessResultViewModel
            {
                Success = false,
                ErrorMessage = ex.Message,
                Ticket = MapTicket(ticket)
            };
        }
    }

    private async Task<Ticket?> ResolveTicketAsync(
        int? ticketId,
        string? ticketNumber,
        CancellationToken cancellationToken)
    {
        if (ticketId is > 0)
        {
            return await _ticketRepository.GetByIdAsync(ticketId.Value, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(ticketNumber))
        {
            return await _ticketRepository.GetByNumberAsync(NormalizeTicketNumber(ticketNumber), cancellationToken);
        }

        return null;
    }

    private static string NormalizeTicketNumber(string ticketNumber)
    {
        var normalized = ticketNumber.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..].Trim();
        }

        return normalized;
    }

    private async Task<bool> ResolveProcessImagesAsync(
        Ticket ticket,
        bool processImagesRequested,
        CancellationToken cancellationToken)
    {
        if (!processImagesRequested || !_ragOptions.EnableImageTextExtraction)
        {
            return false;
        }

        var oldestComment = await _ticketRepository.GetOldestActionWithContentByTicketIdAsync(
            ticket.Id,
            cancellationToken);
        if (oldestComment is null)
        {
            return false;
        }

        var attachments = await _attachmentService.ResolveAttachmentsAsync(
            oldestComment.TeamSupportActionId,
            ticket.TeamSupportId,
            oldestComment.Content,
            cancellationToken);
        var attachmentSources = attachments.Select(item => new CommentImageHelper.CommentImageSource(
            item.OriginalUrl,
            item.FileName,
            item.IsImage));

        var usableImageCount = CommentImageHelper.GetDisplayableImageUrls(
            oldestComment.Content,
            attachmentSources).Count;

        return usableImageCount > 0;
    }

    private static TicketRagProcessTicketViewModel MapTicket(Ticket ticket) => new()
    {
        Id = ticket.Id,
        Number = ticket.Number ?? string.Empty,
        Title = ticket.Title,
        Status = ticket.Status,
        Priority = ticket.Priority,
        TeamSupportId = ticket.TeamSupportId,
        Customer = ticket.Customer,
        CreatedAt = ticket.CreatedAt
    };

    private static TicketRagProcessIndexingViewModel MapIndexing(TicketIndexResult result) => new()
    {
        ChunkCount = result.ChunkCount,
        ProcessImages = result.ProcessImages,
        ImagesDetected = result.ImagesDetected,
        ImagesExtracted = result.ImagesExtracted,
        ImageExtractionWarning = result.ImageExtractionWarning
    };
}
