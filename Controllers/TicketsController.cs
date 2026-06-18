using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Tickets;

namespace MoneyPenny.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private readonly ITicketService _ticketService;
    private readonly ITicketIngestionService _ingestionService;

    public TicketsController(ITicketService ticketService, ITicketIngestionService ingestionService)
    {
        _ticketService = ticketService;
        _ingestionService = ingestionService;
    }

    public async Task<IActionResult> Index(
        string? search,
        string? status,
        string? group,
        string? customer,
        string? product,
        string? estado,
        string? priority,
        string? limit,
        CancellationToken cancellationToken)
    {
        var filters = new TicketFilters
        {
            Search = search,
            StatusText = status,
            Group = group,
            Customer = customer,
            Product = product,
            Status = estado,
            Priority = priority,
            ResultLimit = string.IsNullOrWhiteSpace(limit) ? "50" : limit
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
    public async Task<IActionResult> IndexTicket(int id, CancellationToken cancellationToken)
    {
        await _ingestionService.IndexTicketAsync(id, cancellationToken);
        TempData["SuccessMessage"] = $"Ticket {id} enviado a indexación.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
