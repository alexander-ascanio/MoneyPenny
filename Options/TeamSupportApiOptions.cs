namespace MoneyPenny.Options;

public class TeamSupportApiOptions
{
    public const string SectionName = "ExternalApis:TeamSupport";

    public string[] AttachmentHosts { get; set; } =
    [
        "app.na3.teamsupport.com",
        "na3.files.teamsupport.com",
        "files.teamsupport.com",
        "teamsupport.com"
    ];

    public string? AttachmentCookie { get; set; }
    public string? AttachmentBearerToken { get; set; }
    public string AttachmentReferer { get; set; } = "https://app.na3.teamsupport.com/";
}
