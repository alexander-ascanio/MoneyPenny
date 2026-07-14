using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Extensions;
using MoneyPenny.Models;
using MoneyPenny.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMoneyPennyDatabases(builder.Configuration);
builder.Services.AddMoneyPennyServices(builder.Configuration);
builder.Services.AddMoneyPennyAuthentication(builder.Configuration, builder.Environment);

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

        if (app.Environment.IsDevelopment())
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            await DbSeeder.SeedDataAsync(context, appDbConfig.EnableSeed, userManager, roleManager);
        }
        else
        {
            await DbSeeder.SeedDataAsync(context, appDbConfig.EnableSeed);
        }

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

if (!app.Environment.IsDevelopment())
{
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
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
