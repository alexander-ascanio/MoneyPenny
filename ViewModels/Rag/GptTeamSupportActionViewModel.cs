namespace MoneyPenny.ViewModels.Rag;

public class GptTeamSupportActionViewModel
{
    public string? ActionId { get; init; }
    public string? DescriptionHtml { get; init; }
    public string? CreatorName { get; init; }
    public DateTime? CreatedAt { get; init; }
    public bool IsPrivate { get; init; } = true;
    public string? Source { get; init; }
    public bool LoadedFromApi { get; init; }
    public string? LoadError { get; init; }
    public int? QueryLogId { get; set; }
    public short? Rating { get; set; }
}
