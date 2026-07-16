using System.Globalization;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MoneyPenny.Options;

namespace MoneyPenny.Services.TeamSupport;

public class TeamSupportActionApiClient : ITeamSupportActionApiClient
{
    private static readonly string[] DateFormats =
    [
        "M/d/yyyy h:mm tt",
        "M/d/yyyy h:mm:ss tt",
        "MM/dd/yyyy h:mm tt",
        "MM/dd/yyyy h:mm:ss tt"
    ];

    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TeamSupportApiOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TeamSupportActionApiClient> _logger;

    public TeamSupportActionApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<TeamSupportApiOptions> options,
        IHostEnvironment environment,
        ILogger<TeamSupportActionApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task<TeamSupportActionCreateResult> CreatePrivateCommentAsync(
        string teamSupportTicketId,
        string commentHtml,
        string? creatorName = null,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "Omitida la inserción de comentario privado en TeamSupport fuera de Development (ticket {TicketId}).",
                teamSupportTicketId);
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "La inserción de comentarios privados en TeamSupport solo está permitida en Development."
            };
        }

        if (!_options.EnableGptAnswerPrivateActionInsert)
        {
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "La inserción de comentarios privados está deshabilitada en la configuración."
            };
        }

        if (string.IsNullOrWhiteSpace(teamSupportTicketId))
        {
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "Identificador de ticket TeamSupport no disponible."
            };
        }

        if (string.IsNullOrWhiteSpace(commentHtml))
        {
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "El comentario a insertar está vacío."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.ApiOrganizationId)
            || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "Configure ExternalApis:TeamSupport:ApiOrganizationId y ApiToken."
            };
        }

        var baseUrl = ResolveActionsApiBaseUrl();
        var url = $"{baseUrl}/api/json/tickets/{Uri.EscapeDataString(teamSupportTicketId.Trim())}/actions";
        var author = string.IsNullOrWhiteSpace(creatorName)
            ? _options.GptActionCreatorName
            : creatorName.Trim();

        var payload = new CreateTeamSupportActionRequest
        {
            Action = new CreateTeamSupportActionInput
            {
                Description = commentHtml,
                ActionType = "Comment",
                CreatorName = author,
                ModifierName = author,
                Source = "MoneyPenny",
                IsVisible = "False"
            }
        };

        try
        {
            var client = _httpClientFactory.CreateClient(TeamSupportAttachmentService.HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_options.ApiOrganizationId}:{_options.ApiToken}")));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, RequestJsonOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var actionId = TryReadActionId(body);
                return new TeamSupportActionCreateResult
                {
                    Success = true,
                    ActionId = actionId
                };
            }

            _logger.LogWarning(
                "TeamSupport action API respondió {StatusCode} para ticket {TicketId}: {Body}",
                response.StatusCode,
                teamSupportTicketId,
                body);

            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = response.StatusCode switch
                {
                    HttpStatusCode.NotFound => "Ticket no encontrado en TeamSupport.",
                    HttpStatusCode.Unauthorized => "Credenciales TeamSupport no válidas.",
                    HttpStatusCode.BadRequest => "Solicitud rechazada por la API de TeamSupport.",
                    _ => $"Error al crear el comentario ({(int)response.StatusCode})."
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear comentario privado en TeamSupport para ticket {TicketId}.", teamSupportTicketId);
            return new TeamSupportActionCreateResult
            {
                Success = false,
                ErrorMessage = "No se pudo contactar con la API de TeamSupport."
            };
        }
    }

    public async Task<TeamSupportActionInfo> GetTicketActionAsync(
        string teamSupportTicketId,
        string teamSupportActionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(teamSupportTicketId) || string.IsNullOrWhiteSpace(teamSupportActionId))
        {
            return new TeamSupportActionInfo
            {
                Found = false,
                ErrorMessage = "Identificador de ticket o acción no disponible."
            };
        }

        if (string.IsNullOrWhiteSpace(_options.ApiOrganizationId)
            || string.IsNullOrWhiteSpace(_options.ApiToken))
        {
            return new TeamSupportActionInfo
            {
                Found = false,
                ErrorMessage = "Configure ExternalApis:TeamSupport:ApiOrganizationId y ApiToken."
            };
        }

        var baseUrl = ResolveActionsApiBaseUrl();
        var url =
            $"{baseUrl}/api/json/tickets/{Uri.EscapeDataString(teamSupportTicketId.Trim())}/actions/{Uri.EscapeDataString(teamSupportActionId.Trim())}";

        try
        {
            var body = await TryFetchAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return new TeamSupportActionInfo
                {
                    Found = false,
                    ErrorMessage = "No se pudo obtener la acción desde TeamSupport."
                };
            }

            return TryParseAction(body) ?? new TeamSupportActionInfo
            {
                Found = false,
                ErrorMessage = "La respuesta de TeamSupport no contiene una acción válida."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al consultar acción {ActionId} del ticket {TicketId} en TeamSupport.",
                teamSupportActionId,
                teamSupportTicketId);

            return new TeamSupportActionInfo
            {
                Found = false,
                ErrorMessage = "No se pudo contactar con la API de TeamSupport."
            };
        }
    }

    private async Task<string?> TryFetchAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(TeamSupportAttachmentService.HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.ApiOrganizationId}:{_options.ApiToken}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogDebug(
                "TeamSupport action API respondió {StatusCode} para {Url}.",
                response.StatusCode,
                url);
            return null;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static TeamSupportActionInfo? TryParseAction(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("Action", out var actionElement))
            {
                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("Description", out _))
                {
                    actionElement = root;
                }
                else
                {
                    return null;
                }
            }

            var description = ReadString(actionElement, "Description");
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var isVisibleText = ReadString(actionElement, "IsVisible");
            var isPrivate = !ParseTeamSupportBoolean(isVisibleText, defaultValue: true);

            return new TeamSupportActionInfo
            {
                Found = true,
                ActionId = ReadActionId(actionElement),
                DescriptionHtml = description,
                CreatorName = ReadString(actionElement, "CreatorName"),
                CreatedAt = ParseTeamSupportDate(ReadString(actionElement, "DateCreated")),
                IsPrivate = isPrivate,
                Source = ReadString(actionElement, "Source")
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadActionId(JsonElement actionElement)
    {
        if (actionElement.TryGetProperty("ActionID", out var actionIdElement))
        {
            return actionIdElement.ValueKind == JsonValueKind.String
                ? actionIdElement.GetString()
                : actionIdElement.ToString();
        }

        if (actionElement.TryGetProperty("ActionId", out var camelActionId))
        {
            return camelActionId.ValueKind == JsonValueKind.String
                ? camelActionId.GetString()
                : camelActionId.ToString();
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static bool ParseTeamSupportBoolean(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim() switch
        {
            "True" or "true" or "1" => true,
            "False" or "false" or "0" => false,
            _ => defaultValue
        };
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

    private string ResolveActionsApiBaseUrl()
    {
        var configured = _options.ActionsApiBaseUrl?.Trim();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.TrimEnd('/');
        }

        return _options.AppBaseUrl.TrimEnd('/');
    }

    private static string? TryReadActionId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (!root.TryGetProperty("Action", out var action))
            {
                return null;
            }

            if (action.TryGetProperty("ActionID", out var actionIdElement))
            {
                return actionIdElement.ValueKind == JsonValueKind.String
                    ? actionIdElement.GetString()
                    : actionIdElement.ToString();
            }

            if (action.TryGetProperty("ActionId", out var camelActionId))
            {
                return camelActionId.ValueKind == JsonValueKind.String
                    ? camelActionId.GetString()
                    : camelActionId.ToString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private sealed class CreateTeamSupportActionRequest
    {
        public CreateTeamSupportActionInput? Action { get; set; }
    }

    private sealed class CreateTeamSupportActionInput
    {
        public string? Description { get; set; }
        public string? ActionType { get; set; }
        public string? CreatorName { get; set; }
        public string? ModifierName { get; set; }
        public string? TicketStatus { get; set; }
        public string? Source { get; set; }
        public string? AssignedUsername { get; set; }
        public string? IsVisible { get; set; }
    }
}
