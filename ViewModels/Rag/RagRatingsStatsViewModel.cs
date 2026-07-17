using System.Globalization;
using MoneyPenny.Models.Rag;

namespace MoneyPenny.ViewModels.Rag;

public class RagRatingsStatsViewModel
{
    public RagResponseType? ResponseType { get; init; }

    public int GoodCount { get; init; }
    public int BadCount { get; init; }
    public int NotAnswerableCount { get; init; }
    public int TotalCount => GoodCount + BadCount + NotAnswerableCount;

    public double GoodPercent => Percent(GoodCount);
    public double BadPercent => Percent(BadCount);
    public double NotAnswerablePercent => Percent(NotAnswerableCount);

    public IReadOnlyList<RagRatingsStatsMonthViewModel> Months { get; init; } = [];
    public IReadOnlyList<RagRatingsStatsRecentItemViewModel> RecentItems { get; init; } = [];

    private double Percent(int count)
        => TotalCount == 0 ? 0 : Math.Round(count * 100.0 / TotalCount, 1);

    public static RagRatingsStatsViewModel Build(
        RagResponseType? responseType,
        IReadOnlyList<RagRatingMonthlyStatsRow> rows,
        IReadOnlyList<RagQueryLog> recentLogs)
    {
        var culture = CultureInfo.GetCultureInfo("es-ES");

        var months = rows
            .GroupBy(r => new { r.Year, r.Month })
            .OrderByDescending(g => g.Key.Year)
            .ThenByDescending(g => g.Key.Month)
            .Select(g => new RagRatingsStatsMonthViewModel
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                MonthName = culture.TextInfo.ToTitleCase(
                    culture.DateTimeFormat.GetMonthName(g.Key.Month)),
                GoodCount = g.Where(r => r.Rating == RagQueryLog.RatingGood).Sum(r => r.Count),
                BadCount = g.Where(r => r.Rating == RagQueryLog.RatingBad).Sum(r => r.Count),
                NotAnswerableCount = g.Where(r => r.Rating == RagQueryLog.RatingNotAnswerable).Sum(r => r.Count)
            })
            .ToList();

        var recent = recentLogs
            .Select(l => new RagRatingsStatsRecentItemViewModel
            {
                QueryLogId = l.Id,
                TicketId = l.TicketId,
                Rating = l.Rating ?? 0,
                RatedAt = l.RatedAt,
                ResponseType = l.ResponseType,
                AnswerExcerpt = BuildExcerpt(l.Answer)
            })
            .ToList();

        return new RagRatingsStatsViewModel
        {
            ResponseType = responseType,
            GoodCount = rows.Where(r => r.Rating == RagQueryLog.RatingGood).Sum(r => r.Count),
            BadCount = rows.Where(r => r.Rating == RagQueryLog.RatingBad).Sum(r => r.Count),
            NotAnswerableCount = rows.Where(r => r.Rating == RagQueryLog.RatingNotAnswerable).Sum(r => r.Count),
            Months = months,
            RecentItems = recent
        };
    }

    private static string BuildExcerpt(string answer)
    {
        var text = System.Text.RegularExpressions.Regex.Replace(answer, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= 180 ? text : text[..180] + "…";
    }
}

public class RagRatingsStatsMonthViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public int GoodCount { get; init; }
    public int BadCount { get; init; }
    public int NotAnswerableCount { get; init; }
    public int TotalCount => GoodCount + BadCount + NotAnswerableCount;
}

public class RagRatingsStatsRecentItemViewModel
{
    public int QueryLogId { get; init; }
    public int? TicketId { get; init; }
    public short Rating { get; init; }
    public DateTime? RatedAt { get; init; }
    public RagResponseType ResponseType { get; init; }
    public string AnswerExcerpt { get; init; } = string.Empty;
}
