using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MoneyPenny.Options;

namespace MoneyPenny.Services.TeamSupport;

public class TeamSupportTicketApiClient : ITeamSupportTicketApiClient
{
    private static readonly string[] DateFormats =
    [
        "M/d/yyyy h:mm tt",
        "M/d/yyyy h:mm:ss tt",
        "MM/dd/yyyy h:mm tt",
        "MM/dd/yyyy h:mm:ss tt"
    ];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TeamSupportApiOptions _options;
    private readonly ILogger<TeamSupportTicketApiClient> _logger;

    public TeamSupportTicketApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TeamSupportApiOptions> options,
        ILogger<TeamSupportTicketApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TeamSupportTicketInfo> GetTicketAsync(
        string teamSupportTicketId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamSupportTicketId))
        {
            return new TeamSupportTicketInfo
            {
                Found = false,
                ErrorMessage = "Identificador de ticket TeamSupport no disponible."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.ApiOrganizationId)
            || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return new TeamSupportTicketInfo
            {
                Found = false,
                ErrorMessage = "Configure ExternalApis:TeamSupport:ApiOrganizationId y ApiToken."
            };
        }

        var baseUrl = _options.AppBaseUrl.TrimEnd('/');
        var urls =
            new[]
            {
                $"{baseUrl}/api/json/tickets/{teamSupportTicketId}.json",
                $"{baseUrl}/api/json/tickets/{teamSupportTicketId}"
            };

        foreach (var url in urls)
        {
            var body = await TryFetchAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            var parsed = TryParseTicket(body);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        return new TeamSupportTicketInfo
        {
            Found = false,
            ErrorMessage = "No se pudo obtener el ticket desde TeamSupport."
        };
    }

    private async Task<string?> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(TeamSupportAttachmentService.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    System.Text.Encoding.UTF8.GetBytes(
                        $"{_options.ApiOrganizationId}:{_options.ApiToken}")));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "TeamSupport ticket API respondió {StatusCode} para {Url}.",
                    response.StatusCode,
                    url);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error al consultar ticket TeamSupport en {Url}.", url);
            return null;
        }
    }

    private static TeamSupportTicketInfo? TryParseTicket(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("Ticket", out var ticketElement))
            {
                if (root.ValueKind == JsonValueKind.Object
                    && (root.TryGetProperty("TicketNumber", out _)
                        || root.TryGetProperty("Status", out _)))
                {
                    ticketElement = root;
                }
                else
                {
                    return null;
                }
            }

            var ticketNumber = ReadTicketNumber(ticketElement);
            var status = ReadString(ticketElement, "Status");
            var createdAt = ParseTeamSupportDate(ReadString(ticketElement, "DateCreated"));

            if (ticketNumber is null && status is null && createdAt is null)
            {
                return null;
            }

            return new TeamSupportTicketInfo
            {
                Found = true,
                TicketNumber = ticketNumber,
                Status = status,
                CreatedAt = createdAt
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadTicketNumber(JsonElement ticketElement)
    {
        if (!ticketElement.TryGetProperty("TicketNumber", out var numberElement))
        {
            return null;
        }

        return numberElement.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(numberElement.GetString()) ? null : numberElement.GetString(),
            JsonValueKind.Number when numberElement.TryGetInt64(out var number) => number.ToString(CultureInfo.InvariantCulture),
            _ => numberElement.ToString()
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static DateTime? ParseTeamSupportDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParseExact(
                value.Trim(),
                DateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified)
            : null;
    }
}
