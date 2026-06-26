using MoneyPenny.Helpers;
using MoneyPenny.Models.Tickets;
using MoneyPenny.ViewModels.Tickets;
using Microsoft.EntityFrameworkCore;

namespace MoneyPenny.Data.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TicketsDbContext _context;

    public TicketRepository(TicketsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Ticket>> GetAllAsync(
        TicketFilters filters,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tickets.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            query = query.Where(t =>
                t.Number.Contains(filters.Search) ||
                t.Title.Contains(filters.Search) ||
                t.Description.Contains(filters.Search));
        }

        var status = !string.IsNullOrWhiteSpace(filters.Status)
            ? filters.Status
            : filters.StatusText;

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(filters.Group))
        {
            query = query.Where(t => t.Group == filters.Group);
        }

        if (!string.IsNullOrWhiteSpace(filters.Customer))
        {
            query = query.Where(t => t.Customer == filters.Customer);
        }

        if (!string.IsNullOrWhiteSpace(filters.Product))
        {
            query = query.Where(t => t.Product == filters.Product);
        }

        if (!string.IsNullOrWhiteSpace(filters.Priority))
        {
            query = query.Where(t => t.Priority == filters.Priority);
        }

        IQueryable<Ticket> ordered = query.OrderByDescending(t => t.CreatedAt);

        ordered = filters.ResultLimit switch
        {
            "100" => ordered.Take(100),
            "all" => ordered,
            _ => ordered.Take(50)
        };

        return await ordered.ToListAsync(cancellationToken);
    }

    public async Task<TicketFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        return new TicketFilterOptions
        {
            Groups = await GetDistinctAsync(t => t.Group, cancellationToken),
            Customers = await GetDistinctAsync(t => t.Customer, cancellationToken),
            Products = await GetDistinctAsync(t => t.Product, cancellationToken),
            Statuses = await GetDistinctAsync(t => t.Status, cancellationToken),
            Priorities = await GetDistinctAsync(t => t.Priority, cancellationToken)
        };
    }

    public Task<Ticket?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public Task<Ticket?> GetByNumberAsync(string number, CancellationToken cancellationToken = default)
    {
        return _context.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Number == number, cancellationToken);
    }

    public async Task<IReadOnlyList<TicketAction>> GetActionsByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TicketActions
            .AsNoTracking()
            .Where(a => a.TicketId == ticketId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<TicketAction?> GetOldestActionWithContentByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return _context.TicketActions
            .AsNoTracking()
            .Where(a => a.TicketId == ticketId && a.Content != null && a.Content != "")
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountTicketsWithFirstCommentAsync(CancellationToken cancellationToken = default)
    {
        return _context.TicketActions
            .AsNoTracking()
            .Where(a => a.Content != null && a.Content != "")
            .GroupBy(a => a.TicketId)
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TicketFirstCommentRow>> GetFirstCommentsPageAsync(
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return Array.Empty<TicketFirstCommentRow>();
        }

        const string firstCommentsSql = """
            SELECT DISTINCT ON (ta."TicketId")
                t."Id" AS ticket_id,
                t."TicketNumber",
                t."Subject",
                t."ProductName",
                ta."Id" AS action_id,
                ta."Content",
                ta."CreatedAt"
            FROM ticket_actions ta
            INNER JOIN tickets t ON t."Id" = ta."TicketId"
            WHERE ta."Content" IS NOT NULL AND ta."Content" <> ''
            ORDER BY ta."TicketId", ta."CreatedAt" ASC, ta."Id" ASC
            """;

        var sql = $"""
            SELECT
                fc.ticket_id,
                fc."TicketNumber",
                fc."Subject",
                fc."ProductName",
                fc.action_id,
                fc."Content",
                fc."CreatedAt"
            FROM (
                {firstCommentsSql}
            ) fc
            ORDER BY fc.ticket_id
            OFFSET @skip LIMIT @take
            """;

        await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new Npgsql.NpgsqlParameter("skip", skip));
            command.Parameters.Add(new Npgsql.NpgsqlParameter("take", take));

            var rows = new List<TicketFirstCommentRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new TicketFirstCommentRow
                {
                    TicketId = reader.GetInt32(0),
                    TicketNumber = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    Title = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    Product = reader.IsDBNull(3) ? null : reader.GetString(3),
                    TicketActionId = reader.GetInt32(4),
                    Content = reader.GetString(5),
                    ActionCreatedAt = reader.GetDateTime(6)
                });
            }

            return rows;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    public async Task<TicketFirstCommentRow?> GetFirstCommentByTicketNumberAsync(
        string ticketNumber,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeTicketNumber(ticketNumber);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        var ticket = await GetByNumberAsync(normalized, cancellationToken);
        return ticket is null
            ? null
            : await BuildFirstCommentRowAsync(ticket, cancellationToken);
    }

    public async Task<TicketFirstCommentRow?> GetFirstCommentByTicketIdAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await GetByIdAsync(ticketId, cancellationToken);
        return ticket is null
            ? null
            : await BuildFirstCommentRowAsync(ticket, cancellationToken);
    }

    private async Task<TicketFirstCommentRow?> BuildFirstCommentRowAsync(
        Ticket ticket,
        CancellationToken cancellationToken)
    {
        var action = await GetOldestActionWithContentByTicketIdAsync(ticket.Id, cancellationToken);
        if (action is null || string.IsNullOrWhiteSpace(action.Content))
        {
            return null;
        }

        return new TicketFirstCommentRow
        {
            TicketId = ticket.Id,
            TicketNumber = ticket.Number,
            Title = ticket.Title,
            Product = ticket.Product,
            TicketActionId = action.Id,
            Content = action.Content,
            ActionCreatedAt = action.CreatedAt
        };
    }

    public async Task<FirstCommentCorpusStats> GetFirstCommentCorpusStatsAsync(
        int sampleSize,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(sampleSize, 1, 500);
        var charStats = await GetFirstCommentCorpusCharStatsAsync(take, cancellationToken);
        if (charStats.SampleSize == 0)
        {
            return new FirstCommentCorpusStats();
        }

        var imageSampleSize = Math.Min(50, take);
        var imageContents = await GetFirstCommentContentsSampleAsync(imageSampleSize, cancellationToken);
        var totalImages = imageContents.Sum(content => TicketHtmlHelper.ExtractImageSources(content).Count);

        return new FirstCommentCorpusStats
        {
            SampleSize = charStats.SampleSize,
            AverageCharCount = charStats.AverageCharCount,
            AverageImagesPerTicket = imageContents.Count > 0
                ? totalImages / (double)imageContents.Count
                : 0
        };
    }

    private async Task<(int SampleSize, int AverageCharCount)> GetFirstCommentCorpusCharStatsAsync(
        int take,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*)::int,
                COALESCE(AVG(LENGTH(sample."Content"))::int, 0)
            FROM (
                SELECT fc."Content"
                FROM (
                    SELECT DISTINCT ON (ta."TicketId")
                        ta."Content"
                    FROM ticket_actions ta
                    WHERE ta."Content" IS NOT NULL AND ta."Content" <> ''
                    ORDER BY ta."TicketId", ta."CreatedAt" ASC, ta."Id" ASC
                ) fc
                LIMIT @take
            ) sample
            """;

        await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new Npgsql.NpgsqlParameter("take", take));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return (0, 0);
            }

            return (reader.GetInt32(0), reader.GetInt32(1));
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task<IReadOnlyList<string>> GetFirstCommentContentsSampleAsync(
        int take,
        CancellationToken cancellationToken)
    {
        if (take <= 0)
        {
            return [];
        }

        const string sql = """
            SELECT fc."Content"
            FROM (
                SELECT DISTINCT ON (ta."TicketId")
                    ta."Content"
                FROM ticket_actions ta
                WHERE ta."Content" IS NOT NULL AND ta."Content" <> ''
                ORDER BY ta."TicketId", ta."CreatedAt" ASC, ta."Id" ASC
            ) fc
            LIMIT @take
            """;

        await _context.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = sql;
            command.Parameters.Add(new Npgsql.NpgsqlParameter("take", take));

            var contents = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                contents.Add(reader.GetString(0));
            }

            return contents;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private static string NormalizeTicketNumber(string? ticketNumber)
    {
        if (string.IsNullOrWhiteSpace(ticketNumber))
        {
            return string.Empty;
        }

        var normalized = ticketNumber.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..].Trim();
        }

        return normalized;
    }

    private async Task<IReadOnlyList<string>> GetDistinctAsync(
        System.Linq.Expressions.Expression<Func<Ticket, string?>> selector,
        CancellationToken cancellationToken)
    {
        return await _context.Tickets
            .AsNoTracking()
            .Select(selector)
            .Where(value => value != null && value != "")
            .Distinct()
            .OrderBy(value => value)
            .Select(value => value!)
            .ToListAsync(cancellationToken);
    }
}
