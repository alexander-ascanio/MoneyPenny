using Microsoft.Extensions.Configuration;

var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var config = new ConfigurationBuilder()
    .SetBasePath(projectRoot)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var cookie = config["ExternalApis:TeamSupport:AttachmentCookie"] ?? "";
const string actionId = "106751877";
const string ticketId = "30982664";
const string guid = "19325532-f10d-4224-b9c5-0751daa4833e";

using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"https://app.na3.teamsupport.com/action/{actionId}");
client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0");

var urls = new[]
{
    $"https://app.na3.teamsupport.com/api/json/tickets/{ticketId}/actions",
    $"https://app.na3.teamsupport.com/api/json/tickets/{ticketId}/actions/{actionId}",
    $"https://app.na3.teamsupport.com/api/json/actions?ActionID={actionId}",
    $"https://app.na3.teamsupport.com/api/json/actions?TicketID={ticketId}",
    $"https://app.na3.teamsupport.com/api/json/attachments?ActionID={actionId}",
    $"https://app.na3.teamsupport.com/api/json/attachments?ParentID={actionId}",
    $"https://app.na3.teamsupport.com/dc/1/actionattachments/{actionId}",
    $"https://app.na3.teamsupport.com/dc/1/actionattachments?actionId={actionId}",
};

foreach (var url in urls)
{
    await Try(HttpMethod.Get, url);
    await Try(HttpMethod.Post, url);
}

async Task Try(HttpMethod method, string url)
{
    try
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        if (method == HttpMethod.Post)
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if ((int)response.StatusCode is 401 or 302 or 404) return;
        Console.WriteLine($"{method} {(int)response.StatusCode} {url} len={body.Length}");
        if (body.Contains(guid, StringComparison.OrdinalIgnoreCase)
            || body.Contains("/attachments/", StringComparison.OrdinalIgnoreCase))
            Console.WriteLine(body[..Math.Min(body.Length, 1000)]);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{method} {url}: {ex.Message}");
    }
}
