using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoneyPenny.Data;
using MoneyPenny.Extensions;
using MoneyPenny.Models;
using MoneyPenny.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMoneyPennyDatabases(builder.Configuration);
builder.Services.AddMoneyPennyServices(builder.Configuration);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    var identityConfig = builder.Configuration.GetSection("Identity");

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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddControllersWithViews();
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
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

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

        await DbSeeder.SeedDataAsync(context, userManager, roleManager, appDbConfig.EnableSeed);
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

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
