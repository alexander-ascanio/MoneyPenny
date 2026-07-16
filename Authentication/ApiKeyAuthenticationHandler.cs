using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiBearer";
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredToken = _configuration["ApiBearer:Token"];

        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedToken = authHeader["Bearer ".Length..].Trim();

        if (!string.Equals(providedToken, configuredToken, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token no válido."));
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "api-client"),
            new Claim(ClaimTypes.Role, "api"),
            new Claim("auth_method", "api_bearer")
        };

        var identity = new ClaimsIdentity(claims, ApiKeyAuthenticationOptions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ApiKeyAuthenticationOptions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
