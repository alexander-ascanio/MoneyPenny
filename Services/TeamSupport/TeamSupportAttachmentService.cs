using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using MoneyPenny.Helpers;
using MoneyPenny.Options;

namespace MoneyPenny.Services.TeamSupport;

public class TeamSupportAttachmentService : ITeamSupportAttachmentService
{
    public const string HttpClientName = "TeamSupportAttachments";

    private static readonly Regex AttachmentUrlRegex = new(
        @"https?://(?:app\.na3\.teamsupport\.com|na3\.files\.teamsupport\.com|files\.teamsupport\.com)[^\s""'<>]+?/attachments/[0-9a-f-]{36}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttachmentPathRegex = new(
        @"/(?:dc/[^/]+/)?attachments/[0-9a-f-]{36}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TeamSupportApiOptions _options;
    private readonly ILogger<TeamSupportAttachmentService> _logger;

    public TeamSupportAttachmentService(
        IHttpClientFactory httpClientFactory,
        IOptions<TeamSupportApiOptions> options,
        ILogger<TeamSupportAttachmentService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public IReadOnlyList<string> ExtractUrlsFromHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var html = TicketHtmlHelper.PrepareCommentHtml(content);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in TicketHtmlHelper.ExtractImageSources(content))
        {
            if (IsAllowedAttachmentUrl(source))
            {
                urls.Add(source);
            }
        }

        foreach (Match match in AttachmentUrlRegex.Matches(html))
        {
            urls.Add(TicketHtmlHelper.SanitizeImageSource(match.Value));
        }

        foreach (Match match in AttachmentPathRegex.Matches(html))
        {
            var path = match.Value.Trim('"', '\'');
            urls.Add($"{_options.FilesBaseUrl.TrimEnd('/')}{path}");
            urls.Add($"{_options.AppBaseUrl.TrimEnd('/')}{path}");
        }

        return urls.ToArray();
    }

    public async Task<IReadOnlyList<TeamSupportAttachmentInfo>> ResolveAttachmentsAsync(
        string? teamSupportActionId,
        string? teamSupportTicketId,
        string? content,
        CancellationToken cancellationToken = default)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in ExtractUrlsFromHtml(content))
        {
            urls.Add(url);
        }

        foreach (var url in GetOverrideUrls(teamSupportActionId))
        {
            urls.Add(url);
        }

        foreach (var url in await DiscoverUrlsAsync(teamSupportActionId, teamSupportTicketId, cancellationToken))
        {
            urls.Add(url);
        }

        var overrideNames = GetOverrideFileNames(teamSupportActionId);

        return urls
            .Where(IsAllowedAttachmentUrl)
            .Where(url =>
            {
                overrideNames.TryGetValue(url, out var overrideFileName);
                return TicketHtmlHelper.ShouldDisplayAsCommentAttachment(url, overrideFileName);
            })
            .Select(url => new TeamSupportAttachmentInfo
            {
                OriginalUrl = url,
                FileName = overrideNames.TryGetValue(url, out var fileName)
                    ? fileName
                    : TicketHtmlHelper.GuessAttachmentFileName(url),
                IsImage = TicketHtmlHelper.IsLikelyImageAttachmentUrl(url)
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> DiscoverUrlsAsync(
        string? teamSupportActionId,
        string? teamSupportTicketId,
        CancellationToken cancellationToken = default)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            foreach (var url in await DiscoverFromApiAsync(teamSupportActionId, teamSupportTicketId, cancellationToken))
            {
                urls.Add(url);
            }
        }

        if (!HasAuthConfigured())
        {
            return urls.ToArray();
        }

        foreach (var endpoint in BuildDiscoveryEndpoints(teamSupportActionId, teamSupportTicketId))
        {
            var body = await TryFetchTextAsync(endpoint, cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            foreach (Match match in AttachmentUrlRegex.Matches(body))
            {
                urls.Add(TicketHtmlHelper.SanitizeImageSource(match.Value));
            }

            foreach (var url in ExtractRelativeAttachmentUrls(body))
            {
                urls.Add(url);
            }
        }

        return urls.ToArray();
    }

    public async Task<TeamSupportAttachmentDownload?> DownloadAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        if (!IsAllowedAttachmentUrl(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var currentUri = uri;

        for (var redirectCount = 0; redirectCount < 5; redirectCount++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
            ApplyAuthHeaders(request, currentUri);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                await response.Content.CopyToAsync(Stream.Null, cancellationToken);

                if (location is null)
                {
                    return null;
                }

                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "No se pudo descargar adjunto TeamSupport ({StatusCode}) desde {Url}.",
                    response.StatusCode,
                    url);
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (content.Length == 0)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            var fileName = TryGetFileName(response, currentUri);

            return new TeamSupportAttachmentDownload
            {
                Content = content,
                ContentType = contentType,
                FileName = fileName
            };
        }

        return null;
    }

    public bool IsAllowedAttachmentUrl(string url) =>
        Uri.TryCreate(TicketHtmlHelper.SanitizeImageSource(url), UriKind.Absolute, out var uri)
        && IsAllowedAttachmentUri(uri);

    private IEnumerable<string> ExtractRelativeAttachmentUrls(string body)
    {
        foreach (Match match in AttachmentPathRegex.Matches(body))
        {
            var path = match.Value.Trim('"', '\'');
            yield return $"{_options.FilesBaseUrl.TrimEnd('/')}{path}";
            yield return $"{_options.AppBaseUrl.TrimEnd('/')}{path}";
        }
    }

    private async Task<IReadOnlyList<string>> DiscoverFromApiAsync(
        string teamSupportActionId,
        string? teamSupportTicketId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiOrganizationId)
            || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return [];
        }

        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var apiUrl in BuildApiDiscoveryEndpoints(teamSupportActionId, teamSupportTicketId))
        {
            var body = await TryFetchApiTextAsync(apiUrl, cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            foreach (var url in TeamSupportAttachmentJsonParser.ExtractUrls(body, _options))
            {
                urls.Add(url);
            }
        }

        return urls.ToArray();
    }

    private IEnumerable<string> BuildApiDiscoveryEndpoints(string teamSupportActionId, string? teamSupportTicketId)
    {
        var baseUrl = _options.AppBaseUrl.TrimEnd('/');

        yield return $"{baseUrl}/api/json/actions/{teamSupportActionId}";
        yield return $"{baseUrl}/api/json/actions/{teamSupportActionId}/attachments";
        yield return $"{baseUrl}/api/json/attachments?ActionID={teamSupportActionId}";
        yield return $"{baseUrl}/api/json/attachments?ParentID={teamSupportActionId}";

        if (!string.IsNullOrWhiteSpace(teamSupportTicketId))
        {
            yield return $"{baseUrl}/api/json/tickets/{teamSupportTicketId}/actions";
            yield return $"{baseUrl}/api/json/tickets/{teamSupportTicketId}/actions/{teamSupportActionId}";
        }
    }

    private async Task<string?> TryFetchApiTextAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"{_options.ApiOrganizationId}:{_options.ApiToken}")));
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "TeamSupport API adjuntos respondió {StatusCode} para {Url}.",
                    response.StatusCode,
                    url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error al consultar TeamSupport API en {Url}.", url);
            return null;
        }
    }

    private IEnumerable<string> GetOverrideUrls(string? teamSupportActionId)
    {
        if (string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            return [];
        }

        return _options.AttachmentOverrides
            .Where(item => string.Equals(item.TeamSupportActionId, teamSupportActionId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Url.Trim())
            .Where(url => !string.IsNullOrWhiteSpace(url));
    }

    private Dictionary<string, string?> GetOverrideFileNames(string? teamSupportActionId)
    {
        if (string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        return _options.AttachmentOverrides
            .Where(item => string.Equals(item.TeamSupportActionId, teamSupportActionId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Url.Trim(), item => item.FileName, StringComparer.OrdinalIgnoreCase);
    }

    private IEnumerable<string> BuildDiscoveryEndpoints(string? teamSupportActionId, string? teamSupportTicketId)
    {
        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var dc = _options.DataCenterSegment.Trim('/');

        if (!string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            yield return $"{baseUrl}/{dc}/actions/{teamSupportActionId}";
            yield return $"{baseUrl}/{dc}/actions/{teamSupportActionId}/attachments";
            yield return $"{baseUrl}/{dc}/action/{teamSupportActionId}/attachments";
            yield return $"{baseUrl}/{dc}/actionattachments/{teamSupportActionId}";
            yield return $"{_options.FilesBaseUrl.TrimEnd('/')}/{dc}/actions/{teamSupportActionId}/attachments";
        }

        if (!string.IsNullOrWhiteSpace(teamSupportTicketId))
        {
            yield return $"{baseUrl}/{dc}/tickets/{teamSupportTicketId}/attachments";

            if (!string.IsNullOrWhiteSpace(teamSupportActionId))
            {
                yield return $"{baseUrl}/{dc}/tickets/{teamSupportTicketId}/actions/{teamSupportActionId}";
                yield return $"{baseUrl}/{dc}/tickets/{teamSupportTicketId}/actions/{teamSupportActionId}/attachments";
            }
        }
    }

    private async Task<string?> TryFetchTextAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyAuthHeaders(request, uri);
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/html, */*");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error al consultar adjuntos TeamSupport en {Url}.", url);
            return null;
        }
    }

    private void ApplyAuthHeaders(HttpRequestMessage request, Uri uri)
    {
        if (!IsAllowedAttachmentUri(uri)
            && !uri.Host.Contains("teamsupport.com", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_options.AttachmentBearerToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                _options.AttachmentBearerToken);
        }

        if (!string.IsNullOrWhiteSpace(_options.AttachmentCookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _options.AttachmentCookie);
        }

        if (!string.IsNullOrWhiteSpace(_options.AttachmentReferer))
        {
            request.Headers.TryAddWithoutValidation("Referer", _options.AttachmentReferer);
        }
    }

    private bool IsAllowedAttachmentUri(Uri uri) =>
        _options.AttachmentHosts.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));

    private bool HasAuthConfigured() =>
        !string.IsNullOrWhiteSpace(_options.AttachmentCookie)
        || !string.IsNullOrWhiteSpace(_options.AttachmentBearerToken);

    private static string? TryGetFileName(HttpResponseMessage response, Uri uri)
    {
        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrWhiteSpace(lastSegment) ? null : lastSegment;
    }
}
