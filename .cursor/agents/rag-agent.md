# Agente RAG

## Descripción
Especializado en el pipeline RAG de MoneyPenny: tickets → chunks → embeddings → retrieval → respuesta.

## Estructura del proyecto

```
Services/Rag/
  Ingestion/     TicketIngestionService, ChunkingService
  Embeddings/    OpenAiEmbeddingService
  Retrieval/     PgVectorRetrievalService
  Generation/    OpenAiGenerationService
  RagOrchestrator.cs
Data/
  TicketsDbContext.cs    BD origen (solo lectura)
  VectorDbContext.cs     BD vectores
Models/Tickets/          Entidad Ticket
Models/Rag/              DocumentChunk, TicketEmbedding, RagQueryLog
Prompts/                 system.txt, ticket-qa.txt
Options/RagOptions.cs    ChunkSize, TopK, modelos
```

## Orden de implementación

1. Ajustar `Models/Tickets/Ticket.cs` al esquema real de la BD origen
2. Configurar `TicketsDatabase` y `VectorDatabase` en appsettings
3. Crear migración vectorial: `dotnet ef migrations add ... --context VectorDbContext --output-dir Migrations/Vector`
4. Implementar `OpenAiEmbeddingService` y `OpenAiGenerationService`
5. Añadir pgvector y búsqueda en `VectorRepository.SearchSimilarAsync`
6. Probar flujo: Tickets → Indexar → RAG Ask

## Comandos útiles

```bash
dotnet ef migrations add Nombre --context VectorDbContext --output-dir Migrations/Vector
dotnet ef database update --context VectorDbContext
```

## TODOs pendientes en código

- `OpenAiEmbeddingService.CreateEmbeddingAsync`
- `OpenAiGenerationService.GenerateAnswerAsync`
- `VectorRepository.SearchSimilarAsync` (pgvector)
