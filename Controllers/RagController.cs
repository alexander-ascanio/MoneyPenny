using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Models.Rag;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag;
using MoneyPenny.Services.Rag.Export;
using MoneyPenny.Services.Rag.Validation;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Pricing;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.ViewModels.Rag;
using MoneyPenny.ViewModels.Shared;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Security.Claims;

namespace MoneyPenny.Controllers;

[Authorize]
public class RagController : Controller
{
    private readonly IRagOrchestrator _ragOrchestrator;
    private readonly IFirstCommentIndexService _firstCommentIndexService;
    private readonly IFirstCommentBulkIndexJobStore _bulkIndexJobStore;
    private readonly IRagTokenEstimateService _tokenEstimateService;
    private readonly ITicketRepository _ticketRepository;
    private readonly IVectorRepository _vectorRepository;
    private readonly IRagAskResultCache _ragAskResultCache;
    private readonly IResponseGroundingChecker _groundingChecker;
    private readonly IRatedTicketsExportService _ratedTicketsExportService;
    private readonly ITeamSupportActionApiClient _teamSupportActionApiClient;
    private readonly RagOptions _ragOptions;

    public RagController(
        IRagOrchestrator ragOrchestrator,
        IFirstCommentIndexService firstCommentIndexService,
        IFirstCommentBulkIndexJobStore bulkIndexJobStore,
        IRagTokenEstimateService tokenEstimateService,
        ITicketRepository ticketRepository,
        IVectorRepository vectorRepository,
        IRagAskResultCache ragAskResultCache,
        IResponseGroundingChecker groundingChecker,
        IRatedTicketsExportService ratedTicketsExportService,
        ITeamSupportActionApiClient teamSupportActionApiClient,
        IOptions<RagOptions> ragOptions)
    {
        _ragOrchestrator = ragOrchestrator;
        _firstCommentIndexService = firstCommentIndexService;
        _bulkIndexJobStore = bulkIndexJobStore;
        _tokenEstimateService = tokenEstimateService;
        _ticketRepository = ticketRepository;
        _vectorRepository = vectorRepository;
        _ragAskResultCache = ragAskResultCache;
        _groundingChecker = groundingChecker;
        _ratedTicketsExportService = ratedTicketsExportService;
        _teamSupportActionApiClient = teamSupportActionApiClient;
        _ragOptions = ragOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Ask(
        int? ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly = false,
        string? gptResult = null,
        bool focusGpt = false,
        CancellationToken cancellationToken = default)
    {
        if (ticketId is null or <= 0)
        {
            TempData["WarningMessage"] = "Selecciona un ticket para ver la respuesta RAG.";
            return RedirectToAction("Index", "Tickets");
        }

        if (!string.IsNullOrWhiteSpace(gptResult))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
            var cached = _ragAskResultCache.Get(userId, gptResult);
            if (cached is not null)
            {
                return View("Ask", cached.Response);
            }

            TempData["WarningMessage"] = "La respuesta recién generada ya no está en caché. Se mostrará la última respuesta guardada si existe.";
        }

        return await RenderAskAsync(ticketId, ticketNumber, generateGptAnswer: false, knowledgeBaseOnly, focusGpt, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConsultGptAnswer(
        int ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (ticketId <= 0)
        {
            TempData["WarningMessage"] = "Selecciona un ticket para ver la respuesta RAG.";
            return RedirectToAction("Index", "Tickets");
        }

        var notIndexedRedirect = await TryRedirectIfTicketNotIndexedAsync(ticketId, cancellationToken);
        if (notIndexedRedirect is not null)
        {
            return notIndexedRedirect;
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken);
            ticketNumber = ticket?.Number;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        var response = await BuildAskResponseAsync(
            ticketId,
            ticketNumber,
            generateGptAnswer: true,
            knowledgeBaseOnly,
            userId,
            focusGpt: false,
            cancellationToken);

        var cacheKey = _ragAskResultCache.Store(userId, new RagAskCachedResult { Response = response });

        return RedirectToAction(nameof(Ask), new
        {
            ticketId,
            ticketNumber,
            knowledgeBaseOnly,
            gptResult = cacheKey
        });
    }

    [HttpGet]
    public async Task<IActionResult> RatedTickets(
        int page = 1,
        int pageSize = 50,
        RagResponseType? responseType = RagResponseType.Gpt,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            page = 1;
        }

        if (pageSize < 1)
        {
            pageSize = 50;
        }
        else if (pageSize > 200)
        {
            pageSize = 200;
        }

        var result = await _ratedTicketsExportService.GetRatedTicketsAsync(
            page,
            pageSize,
            responseType,
            cancellationToken);

        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RateAnswer(
        int queryLogId,
        short rating,
        CancellationToken cancellationToken = default)
    {
        if (rating is not (RagQueryLog.RatingGood or RagQueryLog.RatingBad or RagQueryLog.RatingNotAnswerable or RagQueryLog.RatingClear))
        {
            return BadRequest(new { success = false, message = "Valoración no válida." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        var saved = await _vectorRepository.RateQueryLogAsync(queryLogId, userId, rating, cancellationToken);

        if (!saved)
        {
            return NotFound(new { success = false, message = "Registro no encontrado." });
        }

        short? storedRating = rating == RagQueryLog.RatingClear ? null : rating;
        return Json(new { success = true, rating = storedRating });
    }

    [HttpGet]
    public async Task<IActionResult> CompareThresholds(
        int? ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (ticketId is null or <= 0)
        {
            TempData["WarningMessage"] = "Selecciona un ticket para comparar umbrales.";
            return RedirectToAction("Index", "Tickets");
        }

        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId.Value, cancellationToken);
            ticketNumber = ticket?.Number;
        }

        return View(new RagThresholdComparisonViewModel
        {
            TicketId = ticketId.Value,
            TicketNumber = ticketNumber,
            KnowledgeBaseOnly = knowledgeBaseOnly,
            ThresholdValues = GetCompareThresholdValues()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompareThresholds(
        int ticketId,
        string? ticketNumber,
        bool knowledgeBaseOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            var ticket = await _ticketRepository.GetByIdAsync(ticketId, cancellationToken);
            ticketNumber = ticket?.Number;
        }

        var model = await _ragOrchestrator.CompareThresholdsAsync(
            ticketId,
            ticketNumber,
            knowledgeBaseOnly,
            cancellationToken);

        return View(model);
    }

    private IReadOnlyList<double> GetCompareThresholdValues()
    {
        var values = _ragOptions.CompareThresholdValues is { Length: > 0 }
            ? _ragOptions.CompareThresholdValues
            : [_ragOptions.MinScore, 0.55, 0.45];

        return values
            .Where(value => value is >= 0 and <= 1)
            .Distinct()
            .OrderByDescending(value => value)
            .Take(3)
            .ToList();
    }

    private async Task<IActionResult> RenderAskAsync(
        int? ticketId,
        string? ticketNumber,
        bool generateGptAnswer,
        bool knowledgeBaseOnly,
        bool focusGpt,
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

        var notIndexedRedirect = await TryRedirectIfTicketNotIndexedAsync(ticketId.Value, cancellationToken);
        if (notIndexedRedirect is not null)
        {
            return notIndexedRedirect;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        var response = await BuildAskResponseAsync(
            ticketId.Value,
            ticketNumber,
            generateGptAnswer,
            knowledgeBaseOnly,
            userId,
            focusGpt,
            cancellationToken);

        return View("Ask", response);
    }

    private async Task<IActionResult?> TryRedirectIfTicketNotIndexedAsync(
        int ticketId,
        CancellationToken cancellationToken)
    {
        if (await _vectorRepository.IsTicketIndexedAsync(ticketId, cancellationToken))
        {
            return null;
        }

        TempData["WarningMessage"] =
            "Este ticket no está indexado. Utiliza «Indexar ticket» antes de consultar la respuesta RAG.";
        return RedirectToAction("Details", "Tickets", new { id = ticketId });
    }

    private async Task<RagResponseViewModel> BuildAskResponseAsync(
        int ticketId,
        string? ticketNumber,
        bool generateGptAnswer,
        bool knowledgeBaseOnly,
        string userId,
        bool focusGpt,
        CancellationToken cancellationToken)
    {
        var response = await _ragOrchestrator.ProcessTicketAsync(
            new AskTicketViewModel
            {
                TicketId = ticketId,
                TicketNumber = ticketNumber,
                GenerateGptAnswer = generateGptAnswer,
                KnowledgeBaseOnly = knowledgeBaseOnly
            },
            userId,
            cancellationToken);

        var contextText = response.GptContextText
            ?? string.Join("\n---\n", response.ContextItems.Select(c => c.Content));
        var contextLoadEstimate = _tokenEstimateService.EstimateRagContextLoad(response.FirstComment?.Content);
        var gptEstimate = _tokenEstimateService.EstimateRagGptAnswer(
            contextText,
            response.FirstComment?.Content);
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
        else if (!generateGptAnswer && string.IsNullOrWhiteSpace(response.Answer))
        {
            await TryApplyStoredGptAnswerAsync(response, ticketId, cancellationToken);
        }

        response.FocusGpt = focusGpt;

        response.RatedAnswers = await LoadRatedAnswersAsync(
            ticketId,
            knowledgeBaseOnly,
            response.GptQueryLogId,
            response.KnowledgeBaseQueryLogId,
            cancellationToken);

        ApplyGroundingReport(response);

        await TryLoadInsertedTeamSupportActionAsync(response, cancellationToken);

        ApplyTeamSupportActionRatingContext(response);

        return response;
    }

    private static void ApplyTeamSupportActionRatingContext(RagResponseViewModel response)
    {
        if (response.InsertedTeamSupportAction is null || response.GptQueryLogId is not > 0)
        {
            return;
        }

        response.InsertedTeamSupportAction.QueryLogId = response.GptQueryLogId;
        response.InsertedTeamSupportAction.Rating = response.GptRating;
    }

    private async Task TryLoadInsertedTeamSupportActionAsync(
        RagResponseViewModel response,
        CancellationToken cancellationToken)
    {
        if (!response.HasGptAnswer || string.IsNullOrWhiteSpace(response.GptTeamSupportActionId))
        {
            return;
        }

        if (response.InsertedTeamSupportAction?.LoadedFromApi == true)
        {
            return;
        }

        var teamSupportTicketId = ResolveTeamSupportTicketIdForAction(response);
        if (string.IsNullOrWhiteSpace(teamSupportTicketId))
        {
            response.InsertedTeamSupportAction = new GptTeamSupportActionViewModel
            {
                ActionId = response.GptTeamSupportActionId,
                LoadError = "No se pudo resolver el identificador del ticket en TeamSupport."
            };
            return;
        }

        var actionInfo = await _teamSupportActionApiClient.GetTicketActionAsync(
            teamSupportTicketId,
            response.GptTeamSupportActionId,
            cancellationToken);

        response.InsertedTeamSupportAction = MapTeamSupportAction(actionInfo, response.GptTeamSupportActionId);
    }

    private static string? ResolveTeamSupportTicketIdForAction(RagResponseViewModel response)
    {
        if (!string.IsNullOrWhiteSpace(response.FirstComment?.TeamSupportTicketId))
        {
            return response.FirstComment.TeamSupportTicketId;
        }

        if (!string.IsNullOrWhiteSpace(response.TicketNumber))
        {
            return response.TicketNumber;
        }

        return response.TicketId > 0 ? response.TicketId.ToString() : null;
    }

    private static GptTeamSupportActionViewModel MapTeamSupportAction(
        TeamSupportActionInfo actionInfo,
        string? fallbackActionId)
    {
        if (!actionInfo.Found)
        {
            return new GptTeamSupportActionViewModel
            {
                ActionId = fallbackActionId,
                LoadError = actionInfo.ErrorMessage ?? "No se pudo cargar el comentario desde TeamSupport."
            };
        }

        return new GptTeamSupportActionViewModel
        {
            ActionId = actionInfo.ActionId ?? fallbackActionId,
            DescriptionHtml = actionInfo.DescriptionHtml,
            CreatorName = actionInfo.CreatorName,
            CreatedAt = actionInfo.CreatedAt,
            IsPrivate = actionInfo.IsPrivate,
            Source = actionInfo.Source,
            LoadedFromApi = true
        };
    }

    private void ApplyGroundingReport(RagResponseViewModel response)
    {
        if (!response.HasGptAnswer || string.IsNullOrWhiteSpace(response.Answer))
        {
            return;
        }

        response.GroundingReport = _groundingChecker.Evaluate(new ResponseGroundingRequest
        {
            Answer = response.Answer,
            FirstCommentContent = response.FirstComment?.Content,
            ContextItems = response.ContextItems,
            TicketNumber = response.TicketNumber,
            KnowledgeBaseSolutionText = response.KnowledgeBaseSolution?.Text
        });
    }

    private async Task TryApplyStoredGptAnswerAsync(
        RagResponseViewModel response,
        int ticketId,
        CancellationToken cancellationToken)
    {
        var log = await _vectorRepository.GetLatestQueryLogByTicketAsync(
            ticketId,
            RagResponseType.Gpt,
            cancellationToken);

        if (log is null || string.IsNullOrWhiteSpace(log.Answer))
        {
            return;
        }

        response.Answer = log.Answer;
        response.HasGptAnswer = true;
        response.GptQueryLogId = log.Id;
        response.GptRating = log.Rating;
        response.GptAnswerFromHistory = true;
        response.GptAnswerSavedAt = log.CreatedAt;
        response.GptTeamSupportActionId = log.TeamSupportActionId;
        response.GptTeamSupportActionInserted = !string.IsNullOrWhiteSpace(log.TeamSupportActionId);
    }

    private async Task<IReadOnlyList<RagRatedAnswerViewModel>> LoadRatedAnswersAsync(
        int ticketId,
        bool knowledgeBaseOnly,
        int? currentGptLogId,
        int? currentKbLogId,
        CancellationToken cancellationToken)
    {
        var responseType = knowledgeBaseOnly ? RagResponseType.KnowledgeBase : RagResponseType.Gpt;
        var excludeIds = new HashSet<int>();
        if (currentGptLogId is > 0)
        {
            excludeIds.Add(currentGptLogId.Value);
        }

        if (currentKbLogId is > 0)
        {
            excludeIds.Add(currentKbLogId.Value);
        }

        var logs = await _vectorRepository.GetRatedQueryLogsByTicketAsync(
            ticketId,
            responseType,
            cancellationToken);

        return logs
            .Where(l => !excludeIds.Contains(l.Id))
            .Select(l => new RagRatedAnswerViewModel
            {
                QueryLogId = l.Id,
                Answer = l.Answer,
                Rating = l.Rating!.Value,
                ResponseType = l.ResponseType,
                CreatedAt = l.CreatedAt,
                RatedAt = l.RatedAt ?? l.CreatedAt
            })
            .ToList();
    }

    [HttpGet]
    public IActionResult FirstCommentIndex(string? jobId)
    {
        var model = new FirstCommentIndexViewModel
        {
            PricingConfig = BuildPricingConfig()
        };

        if (!string.IsNullOrWhiteSpace(jobId))
        {
            var userId = GetCurrentUserId();
            var job = _bulkIndexJobStore.GetJob(userId, jobId);
            if (job?.Status == FirstCommentBulkIndexJobStatus.Completed && job.Result is not null)
            {
                ApplyFirstCommentIndexResult(model, job.Result);
                return View(model);
            }

            if (job?.Status == FirstCommentBulkIndexJobStatus.Failed)
            {
                model.SuccessMessage = $"La indexación falló: {job.ErrorMessage}";
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> FirstCommentCounts(CancellationToken cancellationToken)
    {
        var counts = await _firstCommentIndexService.GetCountsAsync(
            onlyTicketsListScope: true,
            cancellationToken);

        return Json(new
        {
            totalTicketsWithFirstComment = counts.TotalTicketsWithFirstComment,
            indexedTickets = counts.IndexedTickets,
            pendingTickets = counts.PendingTickets,
            knowledgeBaseTotalTicketsWithFirstComment = counts.KnowledgeBaseTotalTicketsWithFirstComment,
            knowledgeBaseIndexedTickets = counts.KnowledgeBaseIndexedTickets,
            knowledgeBasePendingTickets = counts.KnowledgeBasePendingTickets
        });
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

    [HttpGet]
    public async Task<IActionResult> FirstCommentBulkCount(
        bool onlyKnowledgeBaseTickets = false,
        DateTime? ticketCreatedFrom = null,
        DateTime? ticketCreatedTo = null,
        bool rebuildAll = false,
        bool skipAlreadyIndexed = true,
        int? maxTickets = null,
        CancellationToken cancellationToken = default)
    {
        if (ticketCreatedFrom > ticketCreatedTo)
        {
            return BadRequest(new { error = "La fecha hasta debe ser igual o posterior a la fecha desde." });
        }

        var count = await _firstCommentIndexService.CountBulkTicketsToProcessAsync(
            new FirstCommentIndexOptions
            {
                OnlyKnowledgeBaseTickets = onlyKnowledgeBaseTickets,
                TicketCreatedFrom = ticketCreatedFrom,
                TicketCreatedTo = ticketCreatedTo,
                RebuildAll = rebuildAll,
                SkipAlreadyIndexed = skipAlreadyIndexed,
                MaxTickets = maxTickets
            },
            cancellationToken);

        return Json(new { ticketsToProcess = count });
    }

    [HttpGet]
    public async Task<IActionResult> FirstCommentIndexedByMonth(
        bool knowledgeBaseScope = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await _firstCommentIndexService.GetIndexedTicketsByMonthAsync(
            knowledgeBaseScope,
            cancellationToken);

        return Json(new
        {
            scopeTitle = knowledgeBaseScope ? "Knowledge Base" : "Tickets del listado",
            totalTickets = rows.Sum(row => row.TicketCount),
            totalTicketsFormatted = FormatCount(rows.Sum(row => row.TicketCount)),
            months = rows.Select(row => new
            {
                monthName = row.MonthName,
                year = row.Year,
                ticketCount = row.TicketCount,
                ticketCountFormatted = FormatCount(row.TicketCount)
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult FirstCommentIndexStart(FirstCommentIndexViewModel model)
    {
        if (model.TicketCreatedFrom > model.TicketCreatedTo)
        {
            return BadRequest(new { error = "La fecha hasta debe ser igual o posterior a la fecha desde." });
        }

        if (_bulkIndexJobStore.HasActiveJob(GetCurrentUserId()))
        {
            return Conflict(new { error = "Ya hay una indexación masiva en curso." });
        }

        try
        {
            var jobId = _bulkIndexJobStore.StartJob(
                GetCurrentUserId(),
                new FirstCommentIndexOptions
                {
                    RebuildAll = model.RebuildAll,
                    SkipAlreadyIndexed = model.SkipAlreadyIndexed,
                    ProcessImages = model.ProcessImages,
                    OnlyKnowledgeBaseTickets = model.OnlyKnowledgeBaseTickets,
                    MaxTickets = model.MaxTickets,
                    TicketCreatedFrom = model.TicketCreatedFrom,
                    TicketCreatedTo = model.TicketCreatedTo
                });

            return Json(new { jobId });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult FirstCommentIndexProgress(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return NotFound();
        }

        var job = _bulkIndexJobStore.GetJob(GetCurrentUserId(), jobId);
        if (job is null)
        {
            return NotFound();
        }

        return Json(new
        {
            status = job.Status.ToString(),
            phase = job.Phase,
            totalTickets = job.TotalTickets,
            processed = job.Processed,
            indexed = job.Indexed,
            skipped = job.Skipped,
            failed = job.Failed,
            chunksCreated = job.ChunksCreated,
            currentTicketNumber = job.CurrentTicketNumber,
            percentComplete = job.PercentComplete,
            errorMessage = job.ErrorMessage
        });
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
            model.PricingConfig = BuildPricingConfig();
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

        model.PricingConfig = BuildPricingConfig();
        ApplyFirstCommentIndexResult(model, result);
        return View("FirstCommentIndex", model);
    }

    private void ApplyFirstCommentIndexResult(
        FirstCommentIndexViewModel model,
        FirstCommentIndexResult result)
    {
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
                ?? $"Indexación finalizada con {FormatCount(result.TicketsFailed)} error(es).";
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
                    ? $" Imágenes: {FormatCount(result.ImagesExtracted)}/{FormatCount(result.ImagesDetected)} con texto extraído."
                    : " Sin imágenes detectadas en el comentario."
                : string.Empty;

            model.SuccessMessage =
                $"Indexación completada: {FormatCount(result.TicketsIndexed)} ticket(s) indexados, {FormatCount(result.ChunksCreated)} chunk(s), {FormatCount(result.EmbeddingsCreated)} embedding(s).{imageSummary}";

            if (!string.IsNullOrWhiteSpace(result.ImageExtractionWarning))
            {
                model.SuccessMessage += $" Aviso Vision: {result.ImageExtractionWarning}";
            }
        }
        else
        {
            model.SuccessMessage = "No se indexó ningún ticket.";
        }
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

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";

    private static string FormatCount(int value) =>
        value.ToString("N0", CultureInfo.GetCultureInfo("es-ES"));
}
