using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Tickets;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly ITicketIngestionService _ingestionService;
    private readonly TeamSupportApiOptions _teamSupportOptions;

    public TicketsController(
        ITicketService ticketService,
        ITicketIngestionService ingestionService,
        IOptions<TeamSupportApiOptions> teamSupportOptions)
    {
        _ticketService = ticketService;
        _ingestionService = ingestionService;
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
