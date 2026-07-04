using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.Services.Tickets;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly ITicketIngestionService _ingestionService;
    private readonly ITeamSupportAttachmentService _attachmentService;
    private readonly TeamSupportApiOptions _teamSupportOptions;

    public TicketsController(
        ITicketService ticketService,
        ITicketIngestionService ingestionService,
        ITeamSupportAttachmentService attachmentService,
        IOptions<TeamSupportApiOptions> teamSupportOptions)
    {
        _ticketService = ticketService;
        _ingestionService = ingestionService;
        _attachmentService = attachmentService;
        _teamSupportOptions = teamSupportOptions.Value;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? group,
        string? groupName,
        string? customer,
        string? product,
        string? estado,
        string? priority,
        string? isKnowledgeBase,
        string? limit,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken)
    {
        var filters = new TicketFilters
        {
            Search = search,
            StatusText = status,
            GroupName = string.IsNullOrWhiteSpace(groupName) ? group : groupName,
            Customer = customer,
            Product = product,
            Status = estado,
            Priority = priority,
            IsKnowledgeBase = isKnowledgeBase,
            ResultLimit = string.IsNullOrWhiteSpace(limit) ? "50" : limit,
            SortBy = sortBy,
            SortDir = string.IsNullOrWhiteSpace(sortDir) ? "desc" : sortDir
        };

        var model = await _ticketService.GetListAsync(filters, cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _ticketService.GetDetailAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(model);
    }

    public async Task<IActionResult> Attachment(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url) || !_attachmentService.IsAllowedAttachmentUrl(url))
        {
            return BadRequest();
        }

        var download = await _attachmentService.DownloadAsync(url, cancellationToken);
        if (download is null)
        {
            if (string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentCookie)
                && string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentBearerToken))
            {
                return StatusCode(
                    StatusCodes.Status502BadGateway,
                    "No hay credenciales de TeamSupport configuradas para descargar adjuntos.");
            }

            return NotFound();
        }

        Response.Headers.CacheControl = "private, max-age=300";

        if (download.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return File(download.Content, download.ContentType);
        }

        return File(download.Content, download.ContentType, download.FileName);
    }

    public async Task<IActionResult> ResolveActionAttachments(
        string teamSupportActionId,
        string? teamSupportTicketId,
        string? content,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            return BadRequest();
        }

        var attachments = await _attachmentService.ResolveAttachmentsAsync(
            teamSupportActionId,
            teamSupportTicketId,
            content,
            cancellationToken);

        return Json(attachments.Select(item => new
        {
            originalUrl = item.OriginalUrl,
            fileName = item.FileName,
            isImage = item.IsImage,
            proxyUrl = Url.Action("Attachment", "Tickets", new { url = item.OriginalUrl })
        }));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IndexTicket(
        int id,
        bool processImages = true,
        CancellationToken cancellationToken = default)
    {
        var result = await _ingestionService.IndexTicketAsync(id, processImages, cancellationToken);

        if (result.ProcessImages && result.ImagesDetected > 0 && result.ImagesExtracted == 0)
        {
            if (string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentCookie)
                && string.IsNullOrWhiteSpace(_teamSupportOptions.AttachmentBearerToken))
            {
                TempData["WarningMessage"] =
                    $"Ticket {id} indexado, pero no se pudo extraer texto de {result.ImagesDetected} imagen(es). " +
                    "Configura ExternalApis:TeamSupport:AttachmentCookie en appsettings.Development.json.";
            }
            else
            {
                var detail = string.IsNullOrWhiteSpace(result.ImageExtractionWarning)
                    ? "La cookie de TeamSupport puede haber caducado: renueva la sesión en el navegador, copia de nuevo las cookies y reinicia la app."
                    : result.ImageExtractionWarning;

                TempData["WarningMessage"] =
                    $"Ticket {id} indexado, pero no se pudo extraer texto de {result.ImagesDetected} imagen(es). {detail}";
            }
        }
        else if (result.ProcessImages && result.ImagesExtracted > 0)
        {
            TempData["SuccessMessage"] =
                $"Ticket {id} indexado con texto extraído de {result.ImagesExtracted} imagen(es).";
        }
        else if (!result.ProcessImages && result.ImagesDetected > 0)
        {
            TempData["SuccessMessage"] =
                $"Ticket {id} indexado sin procesar {result.ImagesDetected} imagen(es) del comentario.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Ticket {id} indexado correctamente ({result.ChunkCount} fragmentos).";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
