using MoneyPenny.Models.Rag;

namespace MoneyPenny.ViewModels.Rag;

public class RagRatingsStatsViewModel
{
    public RagResponseType? ResponseType { get; init; }

    public int GoodCount { get; init; }
    public int BadCount { get; init; }
    public int NotAnswerableCount { get; init; }
    public int TotalCount => GoodCount + BadCount + NotAnswerableCount;

    public double GoodPercent => Percent(GoodCount, TotalCount);
    public double BadPercent => Percent(BadCount, TotalCount);
    public double NotAnswerablePercent => Percent(NotAnswerableCount, TotalCount);

    /// <summary>Total excluyendo los "no valorables" (solo OK + KO).</summary>
    public int RatedCount => GoodCount + BadCount;
    public double GoodPercentExcludingNa => Percent(GoodCount, RatedCount);
    public double BadPercentExcludingNa => Percent(BadCount, RatedCount);

    /// <summary>Serie diaria (solo OK y KO) para los gráficos de evolución.</summary>
    public IReadOnlyList<RagRatingsStatsDayViewModel> DailySeries { get; init; } = [];
    public IReadOnlyList<RagRatingsStatsRecentItemViewModel> RecentItems { get; init; } = [];

    private static double Percent(int count, int total)
        => total == 0 ? 0 : Math.Round(count * 100.0 / total, 1);

    public static RagRatingsStatsViewModel Build(
        RagResponseType? responseType,
        IReadOnlyList<RagRatingDailyStatsRow> rows,
        IReadOnlyList<RagQueryLog> recentLogs)
    {
        var dailySeries = rows
            .Where(r => r.Rating is RagQueryLog.RatingGood or RagQueryLog.RatingBad)
            .GroupBy(r => r.Date.Date)
            .OrderBy(g => g.Key)
            .Select(g => new RagRatingsStatsDayViewModel
            {
                Date = g.Key,
                GoodCount = g.Where(r => r.Rating == RagQueryLog.RatingGood).Sum(r => r.Count),
                BadCount = g.Where(r => r.Rating == RagQueryLog.RatingBad).Sum(r => r.Count)
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
            DailySeries = dailySeries,
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

public class RagRatingsStatsDayViewModel
{
    public DateTime Date { get; init; }
    public int GoodCount { get; init; }
    public int BadCount { get; init; }
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
