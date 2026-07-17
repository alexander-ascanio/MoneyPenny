using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Models.Tickets;
using MoneyPenny.ViewModels.Api;

namespace MoneyPenny.Controllers;

/// <summary>
/// Endpoint público de ingesta: crea un ticket y su acción inicial en teamsupport_db.
/// Excepción documentada a la regla de solo lectura (ver .cursor/rules/teamsupport-readonly.mdc).
/// </summary>
[Route("api/tickets")]
[Authorize(Policy = "ApiOrUser")]
[IgnoreAntiforgeryToken]
public class TicketIngestController : Controller
{
    private const int TeamSupportIdMaxLength = 36;

    private readonly TicketsDbContext _context;
    private readonly ILogger<TicketIngestController> _logger;

    public TicketIngestController(TicketsDbContext context, ILogger<TicketIngestController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] TicketIngestRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Ticket is null || request.Action is null)
        {
            return BadRequest(new
            {
                success = false,
                errors = new[] { "El payload debe incluir los objetos 'ticket' y 'action'." }
            });
        }

        var errors = Validate(request.Ticket, request.Action);
        if (errors.Count > 0)
        {
            return BadRequest(new { success = false, errors });
        }

        var now = DateTime.UtcNow;
        var status = Trimmed(request.Ticket.Status) ?? "New";

        var ticket = new Ticket
        {
            TeamSupportId = Trimmed(request.Ticket.TeamSupportId) ?? Guid.NewGuid().ToString(),
            Number = Trimmed(request.Ticket.TicketNumber),
            Title = request.Ticket.Subject!.Trim(),
            Description = request.Ticket.Description!,
            Status = status,
            Priority = Trimmed(request.Ticket.Priority) ?? "Normal",
            Type = Trimmed(request.Ticket.Type) ?? "Support",
            Source = Trimmed(request.Ticket.Source) ?? "Web",
            Customer = Trimmed(request.Ticket.CustomerName) ?? string.Empty,
            Contacts = Trimmed(request.Ticket.Contacts),
            Assignee = Trimmed(request.Ticket.AssignedToName) ?? string.Empty,
            Group = Trimmed(request.Ticket.GroupName),
            Product = Trimmed(request.Ticket.ProductName),
            CodigoTelegestion = Trimmed(request.Ticket.CodigoTelegestion),
            IsKnowledgeBase = request.Ticket.IsKnowledgeBase ?? false,
            IsClosed = status.StartsWith("Closed", StringComparison.OrdinalIgnoreCase),
            CreatedAt = now,
            UpdatedAt = now
        };

        var action = new TicketAction
        {
            TeamSupportActionId = Trimmed(request.Action.TeamSupportActionId) ?? Guid.NewGuid().ToString(),
            ActionType = Trimmed(request.Action.ActionType) ?? "Description",
            Content = request.Action.Content!,
            CreatedByName = Trimmed(request.Action.CreatedByName) ?? string.Empty,
            IsVisible = request.Action.IsVisible ?? true,
            TicketStatus = status,
            CreatedAt = now
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.Tickets.Add(ticket);
            await _context.SaveChangesAsync(cancellationToken);

            action.TicketId = ticket.Id;
            _context.TicketActions.Add(action);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error al insertar ticket/acción vía API de ingesta.");
            return Conflict(new
            {
                success = false,
                errors = new[]
                {
                    "No se pudo insertar el ticket. Comprueba que teamSupportId y teamSupportActionId no existan ya."
                }
            });
        }

        _logger.LogInformation(
            "Ticket {TicketId} (TeamSupportId {TeamSupportId}) creado vía API de ingesta con acción {ActionId}.",
            ticket.Id, ticket.TeamSupportId, action.Id);

        return StatusCode(StatusCodes.Status201Created, new TicketIngestResponse
        {
            Success = true,
            TicketId = ticket.Id,
            TicketNumber = ticket.Number,
            TeamSupportId = ticket.TeamSupportId,
            ActionId = action.Id,
            TeamSupportActionId = action.TeamSupportActionId
        });
    }

    private static List<string> Validate(TicketIngestTicket ticket, TicketIngestAction action)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ticket.Subject))
        {
            errors.Add("ticket.subject es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(ticket.Description))
        {
            errors.Add("ticket.description es obligatorio.");
        }

        if (Trimmed(ticket.TeamSupportId) is { Length: > TeamSupportIdMaxLength })
        {
            errors.Add($"ticket.teamSupportId no puede superar {TeamSupportIdMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(action.Content))
        {
            errors.Add("action.content es obligatorio.");
        }

        if (Trimmed(action.TeamSupportActionId) is { Length: > TeamSupportIdMaxLength })
        {
            errors.Add($"action.teamSupportActionId no puede superar {TeamSupportIdMaxLength} caracteres.");
        }

        return errors;
    }

    private static string? Trimmed(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
