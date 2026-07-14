namespace MoneyPenny.ViewModels.Home;

public static class ApiDocumentationCatalog
{
    public static ApiDocsViewModel Build() => new()
    {
        Intro = "Endpoints JSON del portal MoneyPenny. Todas las rutas requieren sesión autenticada (cookie ASP.NET Identity). "
                + "Las peticiones POST con [ValidateAntiForgeryToken] deben incluir el token antifalsificación en el encabezado "
                + "RequestVerificationToken o en el formulario __RequestVerificationToken.",
        Groups =
        [
            new ApiEndpointGroupViewModel
            {
                Title = "RAG — Valoraciones",
                Description = "Consulta y registro de valoraciones sobre respuestas generadas.",
                Endpoints =
                [
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Rag/RatedTickets",
                        Summary = "Lista paginada de respuestas valoradas, enriquecida con datos en tiempo real de TeamSupport.",
                        Notes = "Combina rag_query_logs (VectorDatabase) con la API REST de TeamSupport para número, estado y fecha de creación del ticket.",
                        RequestFields =
                        [
                            Field("page", "int", false, "Número de página. Por defecto 1."),
                            Field("pageSize", "int", false, "Tamaño de página (1–200). Por defecto 50."),
                            Field("responseType", "string", false, "Filtro: Gpt (por defecto) o KnowledgeBase.")
                        ],
                        ResponseFields =
                        [
                            Field("page", "int", true, "Página actual devuelta."),
                            Field("pageSize", "int", true, "Tamaño de página aplicado."),
                            Field("totalCount", "int", true, "Total de registros valorados que cumplen el filtro."),
                            Field("totalPages", "int", true, "Total de páginas calculadas."),
                            Field("responseType", "string", false, "Tipo de respuesta filtrado (Gpt o KnowledgeBase)."),
                            Field("items", "array", true, "Lista de valoraciones."),
                            Field("items[].queryLogId", "int", true, "Id del registro en rag_query_logs."),
                            Field("items[].ticketId", "int", false, "Id interno del ticket en TicketsDatabase."),
                            Field("items[].ticketNumber", "string", false, "Número de ticket (API TeamSupport; respaldo local si falla la API)."),
                            Field("items[].ticketCreatedAt", "datetime", false, "Fecha de creación del ticket (API TeamSupport; respaldo local)."),
                            Field("items[].ticketStatus", "string", false, "Estado actual del ticket (API TeamSupport; respaldo local)."),
                            Field("items[].rating", "short", true, "1 = buena, -1 = mala, -2 = no respondible por MoneyPenny."),
                            Field("items[].ratingLabel", "string", true, "Etiqueta legible: good, bad, not_answerable."),
                            Field("items[].ratedAt", "datetime", false, "Fecha UTC en que se guardó la valoración."),
                            Field("items[].teamSupportLookupError", "string", false, "Mensaje de error si no se pudo consultar TeamSupport; null si la consulta fue correcta.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "POST",
                        Path = "/Rag/RateAnswer",
                        Summary = "Guarda o elimina la valoración de una respuesta RAG.",
                        RequiresAntiForgery = true,
                        RequestFields =
                        [
                            Field("queryLogId", "int", true, "Id del registro en rag_query_logs."),
                            Field("rating", "short", true, "1 = buena, -1 = mala, -2 = no respondible, 0 = quitar valoración.")
                        ],
                        ResponseFields =
                        [
                            Field("success", "bool", true, "true si se guardó correctamente."),
                            Field("rating", "short|null", false, "Valoración almacenada; null si se eliminó (rating=0).")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET / POST",
                        Path = "/Rag/ProcessTicket",
                        Summary = "Indexa un ticket, genera respuesta GPT y ejecuta la comprobación técnica (R1–R6).",
                        Notes = "Combina «Indexar ticket» y «Ver respuesta basada en GPT». Si el comentario #1 tiene capturas utilizables (sin firmas/logotipos), extrae su texto con Vision automáticamente; si no, indexa solo el texto. No inserta comentarios en TeamSupport; el proceso termina con la comprobación técnica.",
                        RequestFields =
                        [
                            Field("ticketId", "int", false, "Id interno del ticket. Obligatorio si no se indica ticketNumber."),
                            Field("ticketNumber", "string", false, "Número de ticket TeamSupport (con o sin #). Obligatorio si no se indica ticketId."),
                            Field("processImages", "bool", false, "Permite Vision automática cuando hay capturas utilizables. Por defecto true; pase false para forzar indexación solo texto.")
                        ],
                        ResponseFields =
                        [
                            Field("success", "bool", true, "true si la indexación y la respuesta GPT fueron correctas."),
                            Field("errorMessage", "string", false, "Mensaje de error si success=false."),
                            Field("ticket", "object", false, "Datos del ticket (id, number, title, status, priority, customer, createdAt)."),
                            Field("indexing", "object", false, "Resultado de indexación (chunkCount, processImages, imagesDetected, imagesExtracted)."),
                            Field("gpt", "object", false, "Respuesta GPT (answer, queryLogId, contextTicketCount)."),
                            Field("groundingCheck", "object", false, "Comprobación técnica R1–R6 (verdict, score, verdictLabel, checks, unsupportedClaims, html)."),
                            Field("groundingCheck.html", "string", false, "HTML autocontenido para pegar en un comentario TeamSupport (mismo aspecto que la tarjeta del portal).")
                        ]
                    }
                ]
            },
            new ApiEndpointGroupViewModel
            {
                Title = "RAG — Índice comentario #1",
                Description = "Contadores, estadísticas y progreso de la indexación masiva del primer comentario.",
                Endpoints =
                [
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Rag/FirstCommentCounts",
                        Summary = "Contadores de tickets con primer comentario e indexación (listado y Knowledge Base).",
                        ResponseFields =
                        [
                            Field("totalTicketsWithFirstComment", "int", true, "Tickets del listado con comentario #1."),
                            Field("indexedTickets", "int", true, "Tickets del listado ya indexados."),
                            Field("pendingTickets", "int", true, "Tickets del listado pendientes de indexar."),
                            Field("knowledgeBaseTotalTicketsWithFirstComment", "int", true, "Tickets KB con comentario #1."),
                            Field("knowledgeBaseIndexedTickets", "int", true, "Tickets KB indexados."),
                            Field("knowledgeBasePendingTickets", "int", true, "Tickets KB pendientes.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Rag/FirstCommentCorpusStats",
                        Summary = "Estadísticas del corpus de primeros comentarios (muestra).",
                        RequestFields =
                        [
                            Field("onlyKnowledgeBaseScope", "bool", false, "Si true, solo tickets de Knowledge Base. Por defecto false.")
                        ],
                        ResponseFields =
                        [
                            Field("averageCommentCharCount", "double", true, "Media de caracteres por comentario en la muestra."),
                            Field("averageImagesPerTicket", "double", true, "Media de imágenes por ticket en la muestra."),
                            Field("corpusSampleSize", "int", true, "Tamaño de la muestra analizada (máx. 200).")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Rag/FirstCommentBulkCount",
                        Summary = "Cuenta cuántos tickets procesaría una indexación masiva con los filtros indicados.",
                        RequestFields =
                        [
                            Field("onlyKnowledgeBaseTickets", "bool", false, "Limitar a tickets de Knowledge Base."),
                            Field("ticketCreatedFrom", "date", false, "Fecha mínima de creación del ticket (inclusive)."),
                            Field("ticketCreatedTo", "date", false, "Fecha máxima de creación del ticket (inclusive)."),
                            Field("rebuildAll", "bool", false, "Reindexar aunque ya estén indexados."),
                            Field("skipAlreadyIndexed", "bool", false, "Omitir tickets ya indexados. Por defecto true."),
                            Field("maxTickets", "int", false, "Límite máximo de tickets a procesar.")
                        ],
                        ResponseFields =
                        [
                            Field("ticketsToProcess", "int", true, "Número de tickets que se procesarían.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Rag/FirstCommentIndexedByMonth",
                        Summary = "Distribución de tickets indexados por mes de creación.",
                        RequestFields =
                        [
                            Field("knowledgeBaseScope", "bool", false, "Si true, ámbito Knowledge Base; si false, listado de tickets.")
                        ],
                        ResponseFields =
                        [
                            Field("scopeTitle", "string", true, "Título del ámbito consultado."),
                            Field("totalTickets", "int", true, "Total de tickets indexados en el ámbito."),
                            Field("totalTicketsFormatted", "string", true, "Total formateado (es-ES)."),
                            Field("months", "array", true, "Desglose mensual."),
                            Field("months[].monthName", "string", true, "Nombre del mes."),
                            Field("months[].year", "int", true, "Año."),
                            Field("months[].ticketCount", "int", true, "Tickets indexados en ese mes."),
                            Field("months[].ticketCountFormatted", "string", true, "Conteo formateado.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "POST",
                        Path = "/Rag/FirstCommentIndexStart",
                        Summary = "Inicia una indexación masiva en segundo plano.",
                        RequiresAntiForgery = true,
                        Notes = "Devuelve 409 si ya hay un trabajo activo para el usuario.",
                        RequestFields =
                        [
                            Field("RebuildAll", "bool", false, "Reindexar todos los tickets del filtro."),
                            Field("SkipAlreadyIndexed", "bool", false, "Omitir tickets ya indexados."),
                            Field("ProcessImages", "bool", false, "Extraer texto de imágenes con Vision."),
                            Field("OnlyKnowledgeBaseTickets", "bool", false, "Solo tickets de Knowledge Base."),
                            Field("MaxTickets", "int", false, "Límite de tickets a procesar."),
                            Field("TicketCreatedFrom", "date", false, "Filtro fecha desde."),
                            Field("TicketCreatedTo", "date", false, "Filtro fecha hasta.")
                        ],
                        ResponseFields =
                        [
                            Field("jobId", "string", true, "Identificador del trabajo para consultar progreso.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Rag/FirstCommentIndexProgress",
                        Summary = "Estado y progreso de una indexación masiva.",
                        RequestFields =
                        [
                            Field("jobId", "string", true, "Id devuelto por FirstCommentIndexStart.")
                        ],
                        ResponseFields =
                        [
                            Field("status", "string", true, "Estado: Running, Completed, Failed, etc."),
                            Field("phase", "string", false, "Fase actual del proceso."),
                            Field("totalTickets", "int", true, "Total de tickets a procesar."),
                            Field("processed", "int", true, "Tickets ya procesados."),
                            Field("indexed", "int", true, "Tickets indexados correctamente."),
                            Field("skipped", "int", true, "Tickets omitidos."),
                            Field("failed", "int", true, "Tickets con error."),
                            Field("chunksCreated", "int", true, "Fragmentos vectoriales creados."),
                            Field("currentTicketNumber", "string", false, "Ticket en procesamiento."),
                            Field("percentComplete", "double", true, "Porcentaje de avance (0–100)."),
                            Field("errorMessage", "string", false, "Mensaje de error si status=Failed.")
                        ]
                    }
                ]
            },
            new ApiEndpointGroupViewModel
            {
                Title = "Tickets — Adjuntos e imágenes",
                Description = "Resolución de adjuntos y análisis de capturas en comentarios.",
                Endpoints =
                [
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "GET",
                        Path = "/Tickets/ResolveActionAttachments",
                        Summary = "Resuelve adjuntos de una acción TeamSupport y devuelve URLs de proxy.",
                        RequestFields =
                        [
                            Field("teamSupportActionId", "string", true, "Id de la acción en TeamSupport."),
                            Field("teamSupportTicketId", "string", false, "Id del ticket en TeamSupport (mejora la resolución)."),
                            Field("content", "string", false, "HTML del comentario para detectar enlaces embebidos.")
                        ],
                        ResponseFields =
                        [
                            Field("[].originalUrl", "string", true, "URL original del adjunto."),
                            Field("[].fileName", "string", true, "Nombre del archivo."),
                            Field("[].isImage", "bool", true, "true si es imagen."),
                            Field("[].proxyUrl", "string", true, "URL del proxy autenticado en MoneyPenny.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "POST",
                        Path = "/Tickets/ExtractImageText",
                        Summary = "Extrae texto de una imagen con Tesseract OCR (local).",
                        RequiresAntiForgery = true,
                        RequestFields =
                        [
                            Field("url", "string", true, "URL de la imagen (form-urlencoded).")
                        ],
                        ResponseFields =
                        [
                            Field("text", "string", true, "Texto extraído.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "POST",
                        Path = "/Tickets/ExtractImageTextVision",
                        Summary = "Extrae texto de una imagen con OpenAI Vision.",
                        RequiresAntiForgery = true,
                        Notes = "Requiere Rag:EnableImageTextExtraction=true y URL permitida por el proxy.",
                        RequestFields =
                        [
                            Field("url", "string", true, "URL de la imagen (form-urlencoded).")
                        ],
                        ResponseFields =
                        [
                            Field("text", "string", true, "Texto extraído."),
                            Field("warning", "string", false, "Aviso opcional del servicio Vision.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "POST",
                        Path = "/Tickets/DetectMessageBox",
                        Summary = "Detecta cuadros de diálogo en una captura (OpenCV, local).",
                        RequiresAntiForgery = true,
                        RequestFields =
                        [
                            Field("url", "string", true, "URL de la imagen (form-urlencoded).")
                        ],
                        ResponseFields =
                        [
                            Field("detected", "bool", true, "true si se detectó un message box."),
                            Field("confidence", "double", true, "Confianza de la detección (0–1)."),
                            Field("summary", "string", false, "Resumen del análisis."),
                            Field("titleText", "string", false, "Texto del título detectado."),
                            Field("messageText", "string", false, "Texto del mensaje detectado."),
                            Field("elements", "array", false, "Elementos detectados con type, x, y, width, height, score.")
                        ]
                    },
                    new ApiEndpointViewModel
                    {
                        HttpMethod = "POST",
                        Path = "/Tickets/DetectMessageBoxVision",
                        Summary = "Detecta cuadros de diálogo en una captura con OpenAI Vision.",
                        RequiresAntiForgery = true,
                        Notes = "Requiere Rag:EnableImageTextExtraction=true.",
                        RequestFields =
                        [
                            Field("url", "string", true, "URL de la imagen (form-urlencoded).")
                        ],
                        ResponseFields =
                        [
                            Field("detected", "bool", true, "true si se detectó un message box."),
                            Field("confidence", "double", true, "Confianza de la detección."),
                            Field("summary", "string", false, "Resumen del análisis."),
                            Field("titleText", "string", false, "Texto del título."),
                            Field("messageText", "string", false, "Texto del mensaje."),
                            Field("elements", "array", false, "Elementos con type, x, y, width, height, score.")
                        ]
                    }
                ]
            }
        ]
    };

    private static ApiFieldViewModel Field(string name, string type, bool required, string description) => new()
    {
        Name = name,
        Type = type,
        Required = required,
        Description = description
    };
}
