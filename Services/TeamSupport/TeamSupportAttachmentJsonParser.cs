using System.Text.Json;
using System.Text.RegularExpressions;
using MoneyPenny.Options;

namespace MoneyPenny.Services.TeamSupport;

internal static class TeamSupportAttachmentJsonParser
{
    private static readonly Regex AttachmentUrlRegex = new(
        @"https?://(?:app\.na3\.teamsupport\.com|na3\.files\.teamsupport\.com|files\.teamsupport\.com)[^\s""'<>]+?/attachments/[0-9a-f-]{36}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex AttachmentPathRegex = new(
        @"/(?:dc/[^/]+/)?attachments/[0-9a-f-]{36}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GuidRegex = new(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractUrls(string json, TeamSupportApiOptions options)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in AttachmentUrlRegex.Matches(json))
        {
            urls.Add(match.Value);
        }

        foreach (Match match in AttachmentPathRegex.Matches(json))
        {
            urls.Add($"{options.FilesBaseUrl.TrimEnd('/')}{match.Value}");
            urls.Add($"{options.AppBaseUrl.TrimEnd('/')}{match.Value}");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            CollectFromElement(document.RootElement, options, urls, inAttachmentContext: false);
        }
        catch (JsonException)
        {
            // Respuesta no JSON; ya se aplicó regex sobre texto plano.
        }

        return urls.ToArray();
    }

    private static void CollectFromElement(
        JsonElement element,
        TeamSupportApiOptions options,
        HashSet<string> urls,
        bool inAttachmentContext)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var nextContext = inAttachmentContext
                        || property.Name.Contains("attach", StringComparison.OrdinalIgnoreCase)
                        || property.Name.Contains("file", StringComparison.OrdinalIgnoreCase);

                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        CollectString(property.Value.GetString(), options, urls, nextContext);
                    }
                    else
                    {
                        CollectFromElement(property.Value, options, urls, nextContext);
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectFromElement(item, options, urls, inAttachmentContext);
                }

                break;

            case JsonValueKind.String:
                CollectString(element.GetString(), options, urls, inAttachmentContext);
                break;
        }
    }

    private static void CollectString(
        string? value,
        TeamSupportApiOptions options,
        HashSet<string> urls,
        bool inAttachmentContext)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (Match match in AttachmentUrlRegex.Matches(value))
        {
            urls.Add(match.Value);
        }

        foreach (Match match in AttachmentPathRegex.Matches(value))
        {
            urls.Add($"{options.FilesBaseUrl.TrimEnd('/')}{match.Value}");
        }

        if (inAttachmentContext && GuidRegex.IsMatch(value))
        {
            var dc = options.DataCenterSegment.Trim('/');
            urls.Add($"{options.FilesBaseUrl.TrimEnd('/')}/{dc}/attachments/{value}");
            urls.Add($"{options.AppBaseUrl.TrimEnd('/')}/{dc}/attachments/{value}");
        }
    }
}
