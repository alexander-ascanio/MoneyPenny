using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoneyPenny.Services.Rag;
using MoneyPenny.ViewModels.Rag;
using System.Security.Claims;

namespace MoneyPenny.Controllers;

[Authorize]
public class RagController : Controller
{
    private readonly IRagOrchestrator _ragOrchestrator;

    public RagController(IRagOrchestrator ragOrchestrator)
    {
        _ragOrchestrator = ragOrchestrator;
    }

    [HttpGet]
    public IActionResult Ask(int? ticketId, string? ticketNumber)
    {
        return View(new AskTicketViewModel
        {
            TicketId = ticketId,
            TicketNumber = ticketNumber
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(AskTicketViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name ?? "anonymous";
        var response = await _ragOrchestrator.AskAsync(model, userId, cancellationToken);
        return View("Response", response);
    }
}
