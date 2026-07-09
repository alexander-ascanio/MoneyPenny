namespace MoneyPenny.ViewModels.Rag;

public enum ResponseGroundingVerdict
{
    Pass,
    Warn,
    Fail
}

public class ResponseGroundingCheckItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public ResponseGroundingVerdict Status { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int Points { get; init; }
}

public class ResponseGroundingEvidenceSummaryViewModel
{
    public int FirstCommentChars { get; init; }
    public int ContextTicketCount { get; init; }
    public double MaxSimilarity { get; init; }
    public int SymptomTermsMatched { get; init; }
    public int SymptomTermsTotal { get; init; }
}

public class ResponseGroundingReportViewModel
{
    public ResponseGroundingVerdict Verdict { get; init; }
    public int Score { get; init; }
    public string VerdictLabel { get; init; } = string.Empty;
    public string BannerMessage { get; init; } = string.Empty;
    public IReadOnlyList<ResponseGroundingCheckItemViewModel> Checks { get; init; } = [];
    public IReadOnlyList<string> UnsupportedClaims { get; init; } = [];
    public IReadOnlyList<string> OrphanEntities { get; init; } = [];
    public IReadOnlyList<string> InvalidTicketCitations { get; init; } = [];
    public ResponseGroundingEvidenceSummaryViewModel EvidenceSummary { get; init; } = new();
}
