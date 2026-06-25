SELECT "ChunkIndex", LEFT("Content", 800) AS content_preview
FROM document_chunks
WHERE "TicketId" = 23805
ORDER BY "ChunkIndex";
