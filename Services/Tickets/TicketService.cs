using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.Models.Rag;
using MoneyPenny.Models.Tickets;
using MoneyPenny.Options;
using MoneyPenny.Services.Rag.Ingestion;
using MoneyPenny.Services.Rag.Pricing;
using MoneyPenny.ViewModels.Shared;
using MoneyPenny.ViewModels.Tickets;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Npgsql;

namespace MoneyPenny.Services.Tickets;

public class TicketService : ITicketService
{
    private readonly ITicketRepository _ticketRepository;
    private readonly IVectorRepository _vectorRepository;
    private readonly IMemoryCache _cache;
    private readonly ITicketIngestionService _ingestionService;
    private readonly IRagTokenEstimateService _tokenEstimateService;
    private readonly RagOptions _ragOptions;
    private readonly ILogger<TicketService> _logger;

    private const string FilterOptionsCacheKeyPrefix = "tickets-filter-options";

    public TicketService(
        ITicketRepository ticketRepository,
        IVectorRepository vectorRepository,
        IMemoryCache cache,
        ITicketIngestionService ingestionService,
        IRagTokenEstimateService tokenEstimateService,
        IOptions<RagOptions> ragOptions,
        ILogger<TicketService> logger)
    {
        _ticketRepository = ticketRepository;
        _vectorRepository = vectorRepository;
        _cache = cache;
        _ingestionService = ingestionService;
        _tokenEstimateService = tokenEstimateService;
        _ragOptions = ragOptions.Value;
        _logger = logger;
    }

    public async Task<TicketListViewModel> GetListAsync(
        TicketFilters filters,
        CancellationToken cancellationToken = default)
    {
        var selections = ToFilterSelections(filters);

        try
        {
            var filterOptions = await GetFilterOptionsAsync(filters.IsKnowledgeBaseFilter, cancellationToken);
            var tickets = await _ticketRepository.GetAllAsync(filters, cancellationToken);
            var indexedIds = (await GetIndexedTicketIdsBySourceSafeAsync(
                DocumentChunkSource.TicketDocument,
                cancellationToken)).ToHashSet();
            var firstCommentIndexedIds = (await GetIndexedTicketIdsBySourceSafeAsync(
                DocumentChunkSource.ClientFirstComment,
                cancellationToken)).ToHashSet();

            var items = tickets.Select(t => new TicketListItemViewModel
            {
                Id = t.Id,
                Number = t.Number,
                Title = t.Title,
                Status = t.Status,
                Priority = t.Priority,
                Customer = t.Customer,
                Contacts = t.Contacts,
                TeamSupportId = t.TeamSupportId,
                CodigoTelegestion = t.CodigoTelegestion,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                IsFirstCommentIndexed = firstCommentIndexedIds.Contains(t.Id),
                IsIndexed = indexedIds.Contains(t.Id)
            }).ToList();

            if (TicketSort.IsInMemorySort(filters.SortBy))
            {
                items = ApplyInMemorySort(items, filters.SortBy!, filters.SortDescending);
            }

            return new TicketListViewModel
            {
                Search = filters.Search,
                StatusFilter = filters.StatusText,
                Filters = selections,
                FilterOptions = filterOptions,
                SortBy = filters.SortBy,
                SortDir = filters.SortDir,
                Tickets = items
            };
        }
        catch (PostgresException ex)
        {
            _logger.LogError(ex, "Error SQL al consultar TicketsDatabase.");
            return BuildErrorViewModel(filters, selections, $"Error al consultar tickets: {ex.MessageText}");
        }
        catch (Exception ex) when (IsTicketsDatabaseConnectionError(ex))
        {
            _logger.LogError(ex, "No se pudo conectar a TicketsDatabase.");
            return BuildErrorViewModel(filters, selections, BuildConnectionErrorMessage(ex));
        }
    }

    private async Task<TicketFilterOptions> GetFilterOptionsAsync(
        bool? isKnowledgeBase,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{FilterOptionsCacheKeyPrefix}:{isKnowledgeBase?.ToString() ?? "default"}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15);
            return await _ticketRepository.GetFilterOptionsAsync(isKnowledgeBase, cancellationToken);
        }) ?? new TicketFilterOptions();
    }

    private static TicketFilterSelections ToFilterSelections(TicketFilters filters) => new()
    {
        GroupName = filters.GroupName,
        Customer = filters.Customer,
        Product = filters.Product,
        Status = filters.Status,
        Priority = filters.Priority,
        IsKnowledgeBase = filters.IsKnowledgeBase,
        ResultLimit = filters.ResultLimit,
        SortBy = filters.SortBy,
        SortDir = filters.SortDir
    };

    private static List<TicketListItemViewModel> ApplyInMemorySort(
        List<TicketListItemViewModel> items,
        string sortBy,
        bool descending)
    {
        IEnumerable<TicketListItemViewModel> ordered = sortBy.ToLowerInvariant() switch
        {
            TicketSort.Indexed => descending
                ? items.OrderByDescending(t => t.IsFirstCommentIndexed).ThenByDescending(t => t.CreatedAt)
                : items.OrderBy(t => t.IsFirstCommentIndexed).ThenByDescending(t => t.CreatedAt),
            TicketSort.Rag => descending
                ? items.OrderByDescending(t => t.IsIndexed).ThenByDescending(t => t.CreatedAt)
                : items.OrderBy(t => t.IsIndexed).ThenByDescending(t => t.CreatedAt),
            _ => items
        };

        return ordered.ToList();
    }

    private static TicketListViewModel BuildErrorViewModel(
        TicketFilters filters,
        TicketFilterSelections selections,
        string errorMessage) => new()
    {
        Search = filters.Search,
        StatusFilter = filters.StatusText,
        Filters = selections,
        SortBy = filters.SortBy,
        SortDir = filters.SortDir,
        ErrorMessage = errorMessage
    };

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
            var actions = await _ticketRepository.GetActionsByTicketIdAsync(id, cancellationToken);
            var oldestComment = await _ticketRepository.GetOldestActionWithContentByTicketIdAsync(id, cancellationToken);
            var firstCommentImageCount = oldestComment is null
                ? 0
                : TicketHtmlHelper.ExtractImageSources(oldestComment.Content).Count;

            TokenUsageEstimateViewModel? indexWithoutImagesEstimate = null;
            TokenUsageEstimateViewModel? indexWithImagesEstimate = null;
            try
            {
                var document = await _ingestionService.BuildTicketDocumentAsync(ticket, processImages: false, cancellationToken);
                indexWithoutImagesEstimate = TokenUsageEstimateViewModel.FromEstimate(
                    _tokenEstimateService.EstimateTicketIndex(document, firstCommentImageCount, processImages: false),
                    "Indexar sin imágenes",
                    compact: true);
                indexWithImagesEstimate = TokenUsageEstimateViewModel.FromEstimate(
                    _tokenEstimateService.EstimateTicketIndex(document, firstCommentImageCount, processImages: true),
                    "Indexar con Vision",
                    compact: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo calcular la estimación de tokens para el ticket {TicketId}.", id);
            }

            return new TicketDetailViewModel
            {
                Id = ticket.Id,
                Number = ticket.Number,
                Title = ticket.Title,
                Description = ticket.Description,
                Status = ticket.Status,
                Priority = ticket.Priority,
                Customer = ticket.Customer,
                Contacts = ticket.Contacts,
                TeamSupportId = ticket.TeamSupportId,
                CodigoTelegestion = ticket.CodigoTelegestion,
                Assignee = ticket.Assignee,
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt,
                IsIndexed = isIndexed,
                FirstCommentImageCount = firstCommentImageCount,
                PromptImageProcessingOnIndex = firstCommentImageCount > 0 && _ragOptions.EnableImageTextExtraction,
                IndexWithoutImagesEstimate = indexWithoutImagesEstimate,
                IndexWithImagesEstimate = indexWithImagesEstimate,
                Comments = actions.Select(MapAction).ToList()
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

    private async Task<IReadOnlyList<int>> GetIndexedTicketIdsBySourceSafeAsync(
        DocumentChunkSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _vectorRepository.GetIndexedTicketIdsBySourceAsync(
                source,
                cancellationToken: cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidCatalogName
            or PostgresErrorCodes.UndefinedTable
            or PostgresErrorCodes.UndefinedColumn)
        {
            _logger.LogWarning(
                "Base vectorial no disponible o desactualizada ({SqlState}). Columnas de indexado mostrarán No para todos los tickets.",
                ex.SqlState);
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
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidCatalogName
            or PostgresErrorCodes.UndefinedTable
            or PostgresErrorCodes.UndefinedColumn)
        {
            return false;
        }
        catch (Exception ex) when (IsTicketsDatabaseConnectionError(ex))
        {
            return false;
        }
    }

    private static TicketActionViewModel MapAction(TicketAction action) => new()
    {
        Id = action.Id,
        ActionType = action.ActionType,
        Content = action.Content,
        Author = action.CreatedByName
            ?? action.ModifierName
            ?? action.AssignedUsername
            ?? "Sistema",
        CreatedAt = action.CreatedAt,
        TicketStatus = action.TicketStatus,
        Source = action.Source,
        TimeSpentMinutes = action.TimeSpentMinutes,
        IsVisible = action.IsVisible
    };
}
