using System.Security.Claims;
using System.Text.Json;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using MoneyPenny.Authentication;
using MoneyPenny.Data;
using MoneyPenny.Models;
using MoneyPenny.Options;
using MoneyPenny.Services;

namespace MoneyPenny.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddMoneyPennyAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // Esquema Bearer API (válido en todos los entornos)
        services.AddAuthentication()
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.SchemeName,
                _ => { });

        if (environment.IsDevelopment())
        {
            AddDevelopmentIdentityAuthentication(services, configuration);
        }
        else
        {
            AddProductionAuth0Authentication(services, configuration);
        }

        // Política que acepta usuario autenticado por cookie O por Bearer API
        services.AddAuthorization(options =>
        {
            options.AddPolicy("ApiOrUser", policy =>
                policy.AddAuthenticationSchemes(
                        ApiKeyAuthenticationOptions.SchemeName,
                        environment.IsDevelopment()
                            ? IdentityConstants.ApplicationScheme
                            : CookieAuthenticationDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser());
        });

        return services;
    }

    private static void AddDevelopmentIdentityAuthentication(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            var identityConfig = configuration.GetSection("Identity");

            options.Password.RequireDigit = bool.Parse(identityConfig["Password:RequireDigit"] ?? "true");
            options.Password.RequiredLength = int.Parse(identityConfig["Password:RequiredLength"] ?? "6");
            options.Password.RequireNonAlphanumeric = bool.Parse(identityConfig["Password:RequireNonAlphanumeric"] ?? "false");
            options.Password.RequireUppercase = bool.Parse(identityConfig["Password:RequireUppercase"] ?? "false");
            options.Password.RequireLowercase = bool.Parse(identityConfig["Password:RequireLowercase"] ?? "false");

            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.Parse(identityConfig["Lockout:DefaultLockoutTimeSpan"] ?? "00:05:00");
            options.Lockout.MaxFailedAccessAttempts = int.Parse(identityConfig["Lockout:MaxFailedAccessAttempts"] ?? "5");
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });
    }

    private static void AddProductionAuth0Authentication(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAuth0WebAppAuthentication(options =>
            {
                options.Domain = configuration["Auth0:Domain"]!;
                options.ClientId = configuration["Auth0:ClientId"]!;
                options.ClientSecret = configuration["Auth0:ClientSecret"]!;
                options.Scope = "openid profile email";
            });

        services.Configure<HubRdAccessSettings>(
            configuration.GetSection("HubRdAccess"));

        var hubRdConfig = configuration.GetSection("HubRdAccess");
        services.AddHttpClient<HubRdAccessService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(
                int.Parse(hubRdConfig["TimeoutSeconds"] ?? "10"));
        });

        services.Configure<OpenIdConnectOptions>(
            Auth0Constants.AuthenticationScheme,
            options =>
            {
                options.Events ??= new OpenIdConnectEvents();
                var existingOnTokenValidated = options.Events.OnTokenValidated;

                options.Events.OnTokenValidated = async context =>
                {
                    if (existingOnTokenValidated != null)
                    {
                        await existingOnTokenValidated(context);
                    }

                    if (context.Principal?.Identity is not ClaimsIdentity identity)
                    {
                        return;
                    }

                    var email = identity.FindFirst(ClaimTypes.Email)?.Value
                                ?? identity.FindFirst("email")?.Value;

                    if (string.IsNullOrEmpty(email))
                    {
                        return;
                    }

                    var accessService = context.HttpContext.RequestServices
                        .GetRequiredService<HubRdAccessService>();

                    var accessResult = await accessService.CheckAccessAsync(email);

                    var debugJson = accessResult != null
                        ? JsonSerializer.Serialize(accessResult, new JsonSerializerOptions { WriteIndented = false })
                        : "API call failed (null response)";
                    identity.AddClaim(new Claim("hubrd_debug", debugJson));
                    identity.AddClaim(new Claim("hubrd_checked_at",
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

                    if (accessResult is { HasAccess: true })
                    {
                        var role = !string.IsNullOrEmpty(accessResult.Role)
                            ? accessResult.Role.ToLowerInvariant()
                            : "usuario";
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        identity.AddClaim(new Claim("hubrd_role", role));
                    }
                    else
                    {
                        identity.AddClaim(new Claim("hubrd_access", "denied"));
                        identity.AddClaim(new Claim("hubrd_reason",
                            accessResult?.Reason ?? "api_error"));
                    }
                };
            });

        var revalidateMinutes = int.Parse(hubRdConfig["RevalidateIntervalMinutes"] ?? "30");

        services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme,
            options =>
            {
                options.LoginPath = "/Account/Welcome";
                options.AccessDeniedPath = "/Account/AccessDenied";

                options.Events.OnValidatePrincipal = async context =>
                {
                    if (context.Principal?.Identity is not ClaimsIdentity identity)
                    {
                        return;
                    }

                    var checkedAtStr = identity.FindFirst("hubrd_checked_at")?.Value;
                    if (string.IsNullOrEmpty(checkedAtStr))
                    {
                        return;
                    }

                    var checkedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(checkedAtStr));
                    if (DateTimeOffset.UtcNow - checkedAt < TimeSpan.FromMinutes(revalidateMinutes))
                    {
                        return;
                    }

                    var email = identity.FindFirst(ClaimTypes.Email)?.Value
                                ?? identity.FindFirst("email")?.Value;
                    if (string.IsNullOrEmpty(email))
                    {
                        return;
                    }

                    var accessService = context.HttpContext.RequestServices
                        .GetRequiredService<HubRdAccessService>();
                    var accessResult = await accessService.CheckAccessAsync(email);

                    var claimsToRemove = identity.Claims
                        .Where(c => c.Type is "hubrd_access" or "hubrd_reason" or "hubrd_checked_at"
                                    or "hubrd_debug" or "hubrd_role" || c.Type == ClaimTypes.Role)
                        .ToList();
                    foreach (var c in claimsToRemove)
                    {
                        identity.RemoveClaim(c);
                    }

                    identity.AddClaim(new Claim("hubrd_checked_at",
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()));

                    if (accessResult is { HasAccess: true })
                    {
                        var role = !string.IsNullOrEmpty(accessResult.Role)
                            ? accessResult.Role.ToLowerInvariant()
                            : "usuario";
                        identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        identity.AddClaim(new Claim("hubrd_role", role));
                    }
                    else
                    {
                        identity.AddClaim(new Claim("hubrd_access", "denied"));
                        identity.AddClaim(new Claim("hubrd_reason",
                            accessResult?.Reason ?? "api_error"));
                    }

                    context.ShouldRenew = true;
                };
            });
    }
}
