Las migraciones de VectorDbContext se generan con:

dotnet ef migrations add InitialVectorCreate --context VectorDbContext --output-dir Migrations/Vector
dotnet ef database update --context VectorDbContext

Después activa ApplyMigrationsOnStartup en VectorDatabase si quieres migración automática al arrancar.
