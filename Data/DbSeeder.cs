using Microsoft.AspNetCore.Identity;
using MoneyPenny.Models;

namespace MoneyPenny.Data;

public static class DbSeeder
{
    public static async Task SeedDataAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, bool enableSeed)
    {
        await SeedRolesAsync(roleManager);
        await SeedAdminUserAsync(userManager);
        
        if (!enableSeed) return;

        if (!context.DummyRecords.Any())
        {
            context.DummyRecords.AddRange(
                new DummyRecord { Name = "Registro de prueba 1", CreatedAt = DateTime.UtcNow },
                new DummyRecord { Name = "Registro de prueba 2", CreatedAt = DateTime.UtcNow },
                new DummyRecord { Name = "Registro de prueba 3", CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "User" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task SeedAdminUserAsync(UserManager<ApplicationUser> userManager)
    {
        var adminEmail = "admin@moneypenny.com";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);

        if (adminUser == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                DisplayName = "Administrador",
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, "Admin123!");

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}
