using MoneyPenny.Data.Repositories;
using MoneyPenny.ViewModels.Tickets;
using Npgsql;

namespace MoneyPenny.Services.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IVectorRepository _vectorRepository;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ITicketRepository ticketRepository,
        IVectorRepository vectorRepository,
        ILogger<TicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _vectorRepository = vectorRepository;
        _logger = logger;
    }

    public async Task<TicketListViewModel> GetListAsync(
        string? search = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tickets = await _ticketRepository.GetAllAsync(search, status, cancellationToken);
            var indexedIds = (await GetIndexedTicketIdsSafeAsync(cancellationToken)).ToHashSet();

            return new TicketListViewModel
            {
                Search = search,
                StatusFilter = status,
                Tickets = tickets.Select(t => new TicketListItemViewModel
                {
                    Id = t.Id,
                    Number = t.Number,
                    Title = t.Title,
                    Status = t.Status,
                    Priority = t.Priority,
                    Customer = t.Customer,
                    Contacts = t.Contacts,
                    CreatedAt = t.CreatedAt,
                    IsIndexed = indexedIds.Contains(t.Id)
                }).ToList()
            };
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Error SQL al consultar TicketsDatabase.");
            return new TicketListViewModel
            {
                Search = search,
                StatusFilter = status,
                ErrorMessage = $"Error al consultar tickets: {ex.MessageText}"
            };
        }
        catch (Exception ex) when (IsTicketsDatabaseConnectionError(ex))
        {
            _logger.LogError(ex, "No se pudo conectar a TicketsDatabase.");
            return new TicketListViewModel
            {
                Search = search,
                StatusFilter = status,
                ErrorMessage = BuildConnectionErrorMessage(ex)
            };
        }
    }

    public async Task<TicketDetailViewModel?> GetDetailAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var ticket = await _ticketRepository.GetByIdAsync(id, cancellationToken);
            if (ticket is null)
            {
                return null;
            }

            var isIndexed = await IsTicketIndexedSafeAsync(id, cancellationToken);

            return new TicketDetailViewModel
            {
                Id = ticket.Id,
                Number = ticket.Number,
                Title = ticket.Title,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Assignee = ticket.Assignee,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt,
                IsIndexed = isIndexed
            };
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Error SQL al consultar TicketsDatabase.");
            return new TicketDetailViewModel
            {
                Id = id,
                ErrorMessage = $"Error al consultar el ticket: {ex.MessageText}"
            };
        }
        catch (Exception ex) when (IsTicketsDatabaseConnectionError(ex))
        {
            _logger.LogError(ex, "No se pudo conectar a TicketsDatabase.");
            return new TicketDetailViewModel
            {
                Id = id,
                ErrorMessage = BuildConnectionErrorMessage(ex)
            };
        }
    }

    private static bool IsTicketsDatabaseConnectionError(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException)
            {
                return false;
            }

            if (current is TimeoutException)
            {
                return true;
            }

            if (current is NpgsqlException { SqlState: null } npg
                && (npg.Message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase)
                    || npg.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildConnectionErrorMessage(Exception ex)
    {
        var detail = ex.InnerException?.Message ?? ex.Message;
        return "No se pudo conectar a la base de datos de tickets (TeamSupport). "
            + "Verifica que tu IP esté permitida en el firewall de Azure PostgreSQL "
            + "(servidor tmtpostgresql) y que TicketsDatabase en appsettings.Development.json sea correcto. "
            + $"Detalle: {detail}";
    }

    private async Task<IReadOnlyList<int>> GetIndexedTicketIdsSafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _vectorRepository.GetIndexedTicketIdsAsync(cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidCatalogName or PostgresErrorCodes.UndefinedTable)
        {
            _logger.LogWarning("Base vectorial no disponible aún. Columna Indexado mostrará No para todos los tickets.");
            return [];
        }
        catch (Exception ex) when (IsTicketsDatabaseConnectionError(ex))
        {
            return [];
        }
    }

    private async Task<bool> IsTicketIndexedSafeAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await _vectorRepository.IsTicketIndexedAsync(id, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidCatalogName or PostgresErrorCodes.UndefinedTable)
        {
            return false;
        }
        catch (Exception ex) when (IsTicketsDatabaseConnectionError(ex))
        {
            return false;
        }
    }
}
