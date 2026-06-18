Migraciones de `VectorDbContext` (PostgreSQL local: `moneypenny_vectors_db`).

## Tablas

| Tabla | Descripción |
|---|---|
| `document_chunks` | Fragmentos de texto indexados por ticket |
| `ticket_embeddings` | Vectores de embedding por chunk (`real[]`) |
| `rag_query_logs` | Historial de consultas RAG |

## Comandos

```bash
dotnet ef migrations add Nombre --context VectorDbContext --output-dir Migrations/Vector
dotnet ef database update --context VectorDbContext
```

Con `ApplyMigrationsOnStartup: true` en `VectorDatabase` (appsettings), las migraciones se aplican al arrancar la aplicación.
