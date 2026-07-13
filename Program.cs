using System.Security.Claims;
using System.Text.Json;
using Auth0.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Extensions;
using MoneyPenny.Models;
using MoneyPenny.Options;
using MoneyPenny.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMoneyPennyDatabases(builder.Configuration);
builder.Services.AddMoneyPennyServices(builder.Configuration);

builder.Services
    .AddAuth0WebAppAuthentication(options =>
    {
        options.Domain = builder.Configuration["Auth0:Domain"]!;
        options.ClientId = builder.Configuration["Auth0:ClientId"]!;
        options.ClientSecret = builder.Configuration["Auth0:ClientSecret"]!;
        options.Scope = "openid profile email";
    });

builder.Services.Configure<HubRdAccessSettings>(
    builder.Configuration.GetSection("HubRdAccess"));

var hubRdConfig = builder.Configuration.GetSection("HubRdAccess");
builder.Services.AddHttpClient<HubRdAccessService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(
        int.Parse(hubRdConfig["TimeoutSeconds"] ?? "10"));
})
.ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    if (builder.Environment.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    return handler;
});

builder.Services.Configure<OpenIdConnectOptions>(
    Auth0Constants.AuthenticationScheme, options =>
{
    options.Events ??= new OpenIdConnectEvents();
    var existingOnTokenValidated = options.Events.OnTokenValidated;

    options.Events.OnTokenValidated = async context =>
    {
        if (existingOnTokenValidated != null)
            await existingOnTokenValidated(context);

        if (context.Principal?.Identity is not ClaimsIdentity identity)
            return;

        var email = identity.FindFirst(ClaimTypes.Email)?.Value
                    ?? identity.FindFirst("email")?.Value;

        if (string.IsNullOrEmpty(email))
            return;

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

builder.Services.Configure<CookieAuthenticationOptions>(
    CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/Account/Welcome";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Events.OnValidatePrincipal = async context =>
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
            return;

        var checkedAtStr = identity.FindFirst("hubrd_checked_at")?.Value;
        if (string.IsNullOrEmpty(checkedAtStr))
            return;

        var checkedAt = DateTimeOffset.FromUnixTimeSeconds(long.Parse(checkedAtStr));
        if (DateTimeOffset.UtcNow - checkedAt < TimeSpan.FromMinutes(revalidateMinutes))
            return;

        var email = identity.FindFirst(ClaimTypes.Email)?.Value
                    ?? identity.FindFirst("email")?.Value;
        if (string.IsNullOrEmpty(email))
            return;

        var accessService = context.HttpContext.RequestServices
            .GetRequiredService<HubRdAccessService>();
        var accessResult = await accessService.CheckAccessAsync(email);

        var claimsToRemove = identity.Claims
            .Where(c => c.Type is "hubrd_access" or "hubrd_reason" or "hubrd_checked_at"
                        or "hubrd_debug" or "hubrd_role" || c.Type == ClaimTypes.Role)
            .ToList();
        foreach (var c in claimsToRemove) identity.RemoveClaim(c);

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

var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}
builder.Services.AddMemoryCache();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var appDbConfig = builder.Configuration.GetSection(ApplicationDatabaseOptions.SectionName).Get<ApplicationDatabaseOptions>()
            ?? new ApplicationDatabaseOptions();
        var vectorDbConfig = builder.Configuration.GetSection(VectorDatabaseOptions.SectionName).Get<VectorDatabaseOptions>()
            ?? new VectorDatabaseOptions();

        var context = services.GetRequiredService<ApplicationDbContext>();
        var vectorContext = services.GetRequiredService<VectorDbContext>();

        if (appDbConfig.ApplyMigrationsOnStartup)
        {
            logger.LogInformation("Verificando y creando base de datos de aplicación si no existe...");
            context.Database.Migrate();
            logger.LogInformation("Migraciones de aplicación aplicadas correctamente.");
        }

        if (vectorDbConfig.ApplyMigrationsOnStartup)
        {
            if (vectorContext.Database.GetMigrations().Any())
            {
                logger.LogInformation("Verificando base de datos vectorial...");
                vectorContext.Database.Migrate();
                logger.LogInformation("Migraciones vectoriales aplicadas correctamente.");
            }
            else
            {
                logger.LogWarning(
                    "VectorDbContext no tiene migraciones. Crea la inicial con: dotnet ef migrations add InitialVectorCreate --context VectorDbContext --output-dir Migrations/Vector");
            }
        }

        await DbSeeder.SeedDataAsync(context, appDbConfig.EnableSeed);
        logger.LogInformation("Inicialización de base de datos completada.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar la base de datos. Verifica la configuración de conexión y los permisos del usuario.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Bloquea a usuarios autenticados en Auth0 pero sin acceso según HubRD.
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true
        && user.HasClaim("hubrd_access", "denied"))
    {
        var path = context.Request.Path.Value ?? "";
        var allowedPaths = new[] { "/Account/AccessDenied", "/Account/Logout", "/Account/Welcome" };
        if (!allowedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            context.Response.Redirect("/Account/AccessDenied");
            return;
        }
    }
    await next();
});

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
