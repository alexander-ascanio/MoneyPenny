using MoneyPenny.Models.Rag;
using MoneyPenny.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MoneyPenny.Data.Repositories;

public class CommentImageTextCacheRepository : ICommentImageTextCacheRepository
{
    private readonly VectorDbContext _context;

    public CommentImageTextCacheRepository(VectorDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetByImageSourcesAsync(
        IEnumerable<string> normalizedImageSources,
        CancellationToken cancellationToken = default)
    {
        var sources = normalizedImageSources
            .Where(source => !string.IsNullOrWhiteSpace(source))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sources.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await _context.CommentImageTextCaches
            .AsNoTracking()
            .Where(entry => sources.Contains(entry.ImageSource))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            entry => entry.ImageSource,
            entry => entry.ExtractedText,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(
        int ticketId,
        int ticketActionId,
        string normalizedImageSource,
        string extractedText,
        string visionModel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(normalizedImageSource)
            || string.IsNullOrWhiteSpace(extractedText))
        {
            return;
        }

        normalizedImageSource = CommentImageSourceKey.ForCache(
            TicketHtmlHelper.SanitizeImageSource(normalizedImageSource));
        if (string.IsNullOrWhiteSpace(normalizedImageSource))
        {
            return;
        }

        visionModel = visionModel.Length <= 100 ? visionModel : visionModel[..100];

        var existing = await _context.CommentImageTextCaches
            .FirstOrDefaultAsync(
                entry => entry.ImageSource == normalizedImageSource,
                cancellationToken);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            _context.CommentImageTextCaches.Add(new CommentImageTextCache
            {
                TicketId = ticketId,
                TicketActionId = ticketActionId,
                ImageSource = normalizedImageSource,
                ExtractedText = extractedText,
                VisionModel = visionModel,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            existing.TicketId = ticketId;
            existing.TicketActionId = ticketActionId;
            existing.ExtractedText = extractedText;
            existing.VisionModel = visionModel;
            existing.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
