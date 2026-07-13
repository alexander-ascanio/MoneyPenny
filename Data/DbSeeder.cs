using MoneyPenny.Models;

namespace MoneyPenny.Data;

public static class DbSeeder
{
    public static async Task SeedDataAsync(ApplicationDbContext context, bool enableSeed)
    {
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
}
