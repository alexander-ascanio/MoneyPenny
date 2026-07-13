namespace MoneyPenny.Models;

public class HubRdAccessSettings
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string AppName { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 10;
    public int RevalidateIntervalMinutes { get; set; } = 30;
}
