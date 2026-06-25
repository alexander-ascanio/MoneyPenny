Migraciones de `VectorDbContext` (PostgreSQL local: `moneypenny_vectors_db`).

## Requisito: extensión pgvector

En PostgreSQL local debe estar instalada la extensión **pgvector**. Si `CREATE EXTENSION vector` falla al migrar:

- Windows: instala pgvector compatible con tu versión de PostgreSQL, o usa una imagen Docker con pgvector preinstalado.
- Verifica con: `CREATE EXTENSION IF NOT EXISTS vector;`

## Tablas

| Tabla | Descripción |
|---|---|
| `document_chunks` | Fragmentos de texto indexados por ticket |
| `ticket_embeddings` | Vectores pgvector (`vector(1536)`) por chunk |
| `rag_query_logs` | Historial de consultas RAG |

La búsqueda por similitud usa distancia coseno (`<=>`) e índice HNSW.

## Comandos

```bash
dotnet ef migrations add Nombre --context VectorDbContext --output-dir Migrations/Vector
dotnet ef database update --context VectorDbContext
```

Tras migrar a pgvector, **reindexa los tickets** (los embeddings antiguos con `real[]` vacíos se eliminan en la migración).
