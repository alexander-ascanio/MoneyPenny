using MoneyPenny.Data.Repositories;
using MoneyPenny.Models.Rag;
using MoneyPenny.Services.TeamSupport;
using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Services.Rag.Export;

public class RatedTicketsExportService : IRatedTicketsExportService
{
    private const int MaxApiConcurrency = 5;

    private readonly IVectorRepository _vectorRepository;
    private readonly ITicketRepository _ticketRepository;
    private readonly ITeamSupportTicketApiClient _teamSupportTicketApiClient;

    public RatedTicketsExportService(
        IVectorRepository vectorRepository,
        ITicketRepository ticketRepository,
        ITeamSupportTicketApiClient teamSupportTicketApiClient)
    {
        _vectorRepository = vectorRepository;
        _ticketRepository = ticketRepository;
        _teamSupportTicketApiClient = teamSupportTicketApiClient;
    }

    public async Task<RatedTicketsExportResultViewModel> GetRatedTicketsAsync(
        int page,
        int pageSize,
        RagResponseType? responseType = RagResponseType.Gpt,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await _vectorRepository.CountRatedQueryLogsAsync(responseType, cancellationToken);
        var totalPages = pageSize > 0
            ? (int)Math.Ceiling(totalCount / (double)pageSize)
            : 0;
        var skip = (page - 1) * pageSize;

        var logs = await _vectorRepository.GetRatedQueryLogsPageAsync(
            skip,
            pageSize,
            responseType,
            cancellationToken);

        var ticketIds = logs
            .Where(log => log.TicketId.HasValue)
            .Select(log => log.TicketId!.Value)
            .Distinct()
            .ToList();

        var ticketLookups = await _ticketRepository.GetTicketExportLookupsByIdsAsync(ticketIds, cancellationToken);
        var apiCache = await FetchTeamSupportTicketsAsync(ticketLookups, cancellationToken);

        var items = logs.Select(log => MapItem(log, ticketLookups, apiCache)).ToList();

        return new RatedTicketsExportResultViewModel
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            ResponseType = responseType?.ToString(),
            Items = items
        };
    }

    private async Task<Dictionary<string, TeamSupportTicketInfo>> FetchTeamSupportTicketsAsync(
        IReadOnlyDictionary<int, TicketExportLookup> ticketLookups,
        CancellationToken cancellationToken)
    {
        var apiIds = ticketLookups
            .Select(pair => ResolveTeamSupportApiId(pair.Value, pair.Key))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var cache = new Dictionary<string, TeamSupportTicketInfo>(StringComparer.Ordinal);
        if (apiIds.Count == 0)
        {
            return cache;
        }

        using var semaphore = new SemaphoreSlim(MaxApiConcurrency);
        var tasks = apiIds.Select(async apiId =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var info = await _teamSupportTicketApiClient.GetTicketAsync(apiId, cancellationToken);
                lock (cache)
                {
                    cache[apiId] = info;
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return cache;
    }

    private static RatedTicketExportItemViewModel MapItem(
        RagQueryLog log,
        IReadOnlyDictionary<int, TicketExportLookup> ticketLookups,
        IReadOnlyDictionary<string, TeamSupportTicketInfo> apiCache)
    {
        TicketExportLookup? lookup = null;
        TeamSupportTicketInfo? apiInfo = null;
        string? apiId = null;

        if (log.TicketId is int ticketId && ticketLookups.TryGetValue(ticketId, out var foundLookup))
        {
            lookup = foundLookup;
            apiId = ResolveTeamSupportApiId(foundLookup, ticketId);
            if (!string.IsNullOrWhiteSpace(apiId))
            {
                apiCache.TryGetValue(apiId, out apiInfo);
            }
        }

        var ticketNumber = apiInfo?.Found == true && !string.IsNullOrWhiteSpace(apiInfo.TicketNumber)
            ? apiInfo.TicketNumber
            : lookup?.Number;

        var ticketStatus = apiInfo?.Found == true && !string.IsNullOrWhiteSpace(apiInfo.Status)
            ? apiInfo.Status
            : lookup?.Status;

        var ticketCreatedAt = apiInfo?.Found == true && apiInfo.CreatedAt.HasValue
            ? apiInfo.CreatedAt
            : lookup?.CreatedAt;

        return new RatedTicketExportItemViewModel
        {
            QueryLogId = log.Id,
            TicketId = log.TicketId,
            TicketNumber = ticketNumber,
            TicketCreatedAt = ticketCreatedAt,
            TicketStatus = ticketStatus,
            Rating = log.Rating ?? 0,
            RatingLabel = GetRatingLabel(log.Rating),
            RatedAt = log.RatedAt,
            TeamSupportLookupError = apiInfo?.Found == false ? apiInfo.ErrorMessage : null
        };
    }

    private static string ResolveTeamSupportApiId(TicketExportLookup lookup, int ticketId)
    {
        if (!string.IsNullOrWhiteSpace(lookup.TeamSupportId))
        {
            return lookup.TeamSupportId;
        }

        return ticketId.ToString();
    }

    private static string GetRatingLabel(short? rating) => rating switch
    {
        RagQueryLog.RatingGood => "good",
        RagQueryLog.RatingBad => "bad",
        RagQueryLog.RatingNotAnswerable => "not_answerable",
        _ => "unknown"
    };
}
