using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Pricing;
using MoneyPenny.ViewModels.Rag;
using MoneyPenny.ViewModels.Shared;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace MoneyPenny.Controllers;

[Authorize]
public class RagController : Controller
{
    private readonly IRagOrchestrator _ragOrchestrator;
    private readonly IFirstCommentIndexService _firstCommentIndexService;
    private readonly IRagTokenEstimateService _tokenEstimateService;
    private readonly ITicketRepository _ticketRepository;
    private readonly RagOptions _ragOptions;

    public RagController(
        IRagOrchestrator ragOrchestrator,
        IFirstCommentIndexService firstCommentIndexService,
        IRagTokenEstimateService tokenEstimateService,
        ITicketRepository ticketRepository,
        IOptions<RagOptions> ragOptions)
    {
        _ragOrchestrator = ragOrchestrator;
        _firstCommentIndexService = firstCommentIndexService;
        _tokenEstimateService = tokenEstimateService;
        _ticketRepository = ticketRepository;
        _ragOptions = ragOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Ask(
        int? ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await RenderAskAsync(ticketId, ticketNumber, generateGptAnswer: false, knowledgeBaseOnly, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsultGptAnswer(
        int ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly = false,
        CancellationToken cancellationToken = default)
    {
        return await RenderAskAsync(ticketId, ticketNumber, generateGptAnswer: true, knowledgeBaseOnly, cancellationToken);
    }

    private async Task<IActionResult> RenderAskAsync(
        int? ticketId,
        string? ticketNumber,
        bool generateGptAnswer,
        bool knowledgeBaseOnly,
        CancellationToken cancellationToken)
    {
        if (ticketId is null or <= 0)
        {
            TempData["WarningMessage"] = "Selecciona un ticket para ver la respuesta RAG.";
            return RedirectToAction("Index", "Tickets");
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId.Value, cancellationToken);
            ticketNumber = ticket?.Number;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        var response = await _ragOrchestrator.ProcessTicketAsync(
            new AskTicketViewModel
            {
                TicketId = ticketId.Value,
                TicketNumber = ticketNumber,
                GenerateGptAnswer = generateGptAnswer,
                KnowledgeBaseOnly = knowledgeBaseOnly
            },
            userId,
            cancellationToken);

        var contextText = string.Join("\n---\n", response.ContextItems.Select(c => c.Content));
        var contextLoadEstimate = _tokenEstimateService.EstimateRagContextLoad(response.FirstComment?.Content);
        var gptEstimate = _tokenEstimateService.EstimateRagGptAnswer(contextText);
        var combinedEstimate = _tokenEstimateService.Combine(contextLoadEstimate, gptEstimate);

        response.GptEstimate = TokenUsageEstimateViewModel.FromEstimate(
            combinedEstimate,
            generateGptAnswer
                ? "Consumo estimado de la consulta"
                : "Estimación al pulsar «Consultar respuesta GPT»");

        if (generateGptAnswer && response.HasGptAnswer)
        {
            response.LastRunEstimate = TokenUsageEstimateViewModel.FromEstimate(
                gptEstimate,
                "Consumo estimado de la respuesta GPT");
        }

        return View("Ask", response);
    }

    [HttpGet]
    public async Task<IActionResult> FirstCommentIndex(CancellationToken cancellationToken)
    {
        var counts = await _firstCommentIndexService.GetCountsAsync(
            onlyTicketsListScope: true,
            cancellationToken);
        var model = new FirstCommentIndexViewModel
        {
            TotalTicketsWithFirstComment = counts.TotalTicketsWithFirstComment,
            IndexedTickets = counts.IndexedTickets,
            PendingTickets = counts.PendingTickets,
            KnowledgeBaseTotalTicketsWithFirstComment = counts.KnowledgeBaseTotalTicketsWithFirstComment,
            KnowledgeBaseIndexedTickets = counts.KnowledgeBaseIndexedTickets,
            KnowledgeBasePendingTickets = counts.KnowledgeBasePendingTickets,
            PricingConfig = BuildPricingConfig()
        };
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> FirstCommentCorpusStats(
        CancellationToken cancellationToken,
        bool onlyKnowledgeBaseScope = false)
    {
        const int sampleSize = 200;
        var corpus = await _firstCommentIndexService.GetCorpusStatsAsync(
            sampleSize,
            onlyKnowledgeBaseScope,
            cancellationToken);
        return Json(new
        {
            averageCommentCharCount = corpus.AverageCharCount,
            averageImagesPerTicket = corpus.AverageImagesPerTicket,
            corpusSampleSize = corpus.SampleSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FirstCommentIndex(
        FirstCommentIndexViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.TicketCreatedFrom > model.TicketCreatedTo)
        {
            ModelState.AddModelError(
                nameof(model.TicketCreatedTo),
                "La fecha hasta debe ser igual o posterior a la fecha desde.");
            await PopulateFirstCommentIndexModelAsync(model, cancellationToken);
            return View("FirstCommentIndex", model);
        }

        var result = await _firstCommentIndexService.IndexAllAsync(
            new FirstCommentIndexOptions
            {
                RebuildAll = model.RebuildAll,
                SkipAlreadyIndexed = model.SkipAlreadyIndexed,
                ProcessImages = model.ProcessImages,
                OnlyKnowledgeBaseTickets = model.OnlyKnowledgeBaseTickets,
                MaxTickets = model.MaxTickets,
                TicketCreatedFrom = model.TicketCreatedFrom,
                TicketCreatedTo = model.TicketCreatedTo
            },
            cancellationToken);

        return await RenderFirstCommentIndexAsync(model, result, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexFirstCommentTicket(
        FirstCommentIndexViewModel model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.TargetTicketNumber))
        {
            ModelState.AddModelError(nameof(model.TargetTicketNumber), "Indica un número de ticket.");
            await PopulateFirstCommentIndexModelAsync(model, cancellationToken);
            return View("FirstCommentIndex", model);
        }

        var result = await _firstCommentIndexService.IndexTicketAsync(
            model.TargetTicketNumber,
            new FirstCommentIndexOptions
            {
                SkipAlreadyIndexed = model.SkipAlreadyIndexedSingle,
                ProcessImages = model.ProcessImagesSingle,
                OnlyKnowledgeBaseTickets = model.OnlyKnowledgeBaseTicketsSingle,
                RebuildAll = model.RebuildAllSingle
            },
            cancellationToken);

        return await RenderFirstCommentIndexAsync(model, result, cancellationToken);
    }

    private async Task<IActionResult> RenderFirstCommentIndexAsync(
        FirstCommentIndexViewModel model,
        FirstCommentIndexResult result,
        CancellationToken cancellationToken)
    {
        await PopulateFirstCommentIndexModelAsync(model, cancellationToken);

        model.LastRunEstimate = result.UsageEstimate is null
            ? null
            : TokenUsageEstimateViewModel.FromEstimate(
                result.UsageEstimate,
                "Consumo estimado de la ejecución");
        model.LastResult = new FirstCommentIndexResultViewModel
        {
            TicketsProcessed = result.TicketsProcessed,
            TicketsIndexed = result.TicketsIndexed,
            TicketsSkipped = result.TicketsSkipped,
            TicketsFailed = result.TicketsFailed,
            ChunksCreated = result.ChunksCreated,
            EmbeddingsCreated = result.EmbeddingsCreated,
            ImagesDetected = result.ImagesDetected,
            ImagesExtracted = result.ImagesExtracted,
            ProcessImages = result.ProcessImages,
            ImageExtractionWarning = result.ImageExtractionWarning,
            Errors = result.Errors
        };

        if (result.TicketsFailed > 0)
        {
            model.SuccessMessage = result.Errors.FirstOrDefault()
                ?? $"Indexación finalizada con {result.TicketsFailed} error(es).";
        }
        else if (result.TicketsSkipped > 0 && result.TicketsIndexed == 0)
        {
            model.SuccessMessage = string.IsNullOrWhiteSpace(model.TargetTicketNumber)
                ? "No se indexó ningún ticket (todos omitidos o sin contenido)."
                : $"Ticket #{model.TargetTicketNumber.Trim().TrimStart('#')}: omitido (ya indexado o sin contenido indexable).";
        }
        else if (result.TicketsIndexed > 0)
        {
            var imageSummary = result.ProcessImages
                ? result.ImagesDetected > 0
                    ? $" Imágenes: {result.ImagesExtracted}/{result.ImagesDetected} con texto extraído."
                    : " Sin imágenes detectadas en el comentario."
                : string.Empty;

            model.SuccessMessage =
                $"Indexación completada: {result.TicketsIndexed} ticket(s) indexados, {result.ChunksCreated} chunk(s), {result.EmbeddingsCreated} embedding(s).{imageSummary}";

            if (!string.IsNullOrWhiteSpace(result.ImageExtractionWarning))
            {
                model.SuccessMessage += $" Aviso Vision: {result.ImageExtractionWarning}";
            }
        }
        else
        {
            model.SuccessMessage = "No se indexó ningún ticket.";
        }

        return View("FirstCommentIndex", model);
    }

    private async Task PopulateFirstCommentIndexModelAsync(
        FirstCommentIndexViewModel model,
        CancellationToken cancellationToken)
    {
        var counts = await _firstCommentIndexService.GetCountsAsync(
            onlyTicketsListScope: true,
            cancellationToken);
        model.TotalTicketsWithFirstComment = counts.TotalTicketsWithFirstComment;
        model.IndexedTickets = counts.IndexedTickets;
        model.PendingTickets = counts.PendingTickets;
        model.KnowledgeBaseTotalTicketsWithFirstComment = counts.KnowledgeBaseTotalTicketsWithFirstComment;
        model.KnowledgeBaseIndexedTickets = counts.KnowledgeBaseIndexedTickets;
        model.KnowledgeBasePendingTickets = counts.KnowledgeBasePendingTickets;
        model.PricingConfig = BuildPricingConfig();
    }

    private RagTokenPricingConfigViewModel BuildPricingConfig() => new()
    {
        CharsPerToken = _ragOptions.CharsPerTokenEstimate,
        ChunkSize = _ragOptions.ChunkSize,
        ChunkOverlap = _ragOptions.ChunkOverlap,
        EmbeddingPricePerMillion = _ragOptions.EmbeddingPricePerMillionTokens,
        ChatInputPricePerMillion = _ragOptions.ChatInputPricePerMillionTokens,
        ChatOutputPricePerMillion = _ragOptions.ChatOutputPricePerMillionTokens,
        VisionInputPricePerMillion = _ragOptions.VisionInputPricePerMillionTokens,
        VisionOutputPricePerMillion = _ragOptions.VisionOutputPricePerMillionTokens,
        VisionInputTokensPerImage = _ragOptions.VisionEstimatedInputTokensPerImage,
        VisionOutputTokensPerImage = _ragOptions.VisionEstimatedOutputTokensPerImage,
        ChatOutputTokens = _ragOptions.ChatEstimatedOutputTokens,
        RagAskContextTokens = _ragOptions.RagAskEstimatedContextTokens,
        RagAskSystemTokens = _ragOptions.RagAskEstimatedSystemTokens,
        EmbeddingModel = _ragOptions.EmbeddingModel,
        ChatModel = _ragOptions.ChatModel,
        VisionModel = _ragOptions.VisionModel
    };
}
