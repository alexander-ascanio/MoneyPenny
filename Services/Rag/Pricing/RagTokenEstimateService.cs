using MoneyPenny.Options;
using MoneyPenny.Services.Rag;
using Microsoft.Extensions.Options;

namespace MoneyPenny.Services.Rag.Pricing;

public class RagTokenEstimateService : IRagTokenEstimateService
{
    private readonly RagOptions _options;

    public RagTokenEstimateService(IOptions<RagOptions> options)
    {
        _options = options.Value;
    }

    public int EstimateTokensFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var charsPerToken = Math.Max(1, _options.CharsPerTokenEstimate);
        return Math.Max(1, (int)Math.Ceiling(text.Length / (double)charsPerToken));
    }

    public int EstimateChunkCount(int textLength)
    {
        if (textLength <= 0)
        {
            return 0;
        }

        var chunkSize = Math.Max(100, _options.ChunkSize);
        var overlap = Math.Clamp(_options.ChunkOverlap, 0, chunkSize / 2);
        var step = Math.Max(1, chunkSize - overlap);
        return (int)Math.Ceiling(textLength / (double)step);
    }

    public TokenUsageEstimate EstimateEmbeddingsForTexts(IReadOnlyList<string> texts)
    {
        var totalTokens = 0;
        var apiCalls = 0;
        var chunkSize = Math.Max(100, _options.ChunkSize);
        var overlap = Math.Clamp(_options.ChunkOverlap, 0, chunkSize / 2);
        var step = Math.Max(1, chunkSize - overlap);

        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            for (var start = 0; start < text.Length; start += step)
            {
                var length = Math.Min(chunkSize, text.Length - start);
                totalTokens += EstimateTokensFromText(text.AsSpan(start, length).ToString());
                apiCalls++;

                if (start + length >= text.Length)
                {
                    break;
                }
            }
        }

        if (apiCalls == 0)
        {
            return BuildEstimate(lines: ["Sin contenido indexable."]);
        }

        return BuildEstimate(
            embeddingInputTokens: totalTokens,
            embeddingApiCalls: apiCalls,
            lines:
            [
                $"Embeddings ({_options.EmbeddingModel}): ~{apiCalls:N0} llamada(s), ~{totalTokens:N0} tokens de entrada."
            ]);
    }

    public TokenUsageEstimate EstimateTicketIndex(
        string documentText,
        int imageCount,
        bool processImages)
    {
        var embeddingEstimate = EstimateEmbeddingsForTexts([documentText]);
        if (!processImages || imageCount <= 0)
        {
            return BuildEstimate(
                embeddingInputTokens: embeddingEstimate.EmbeddingInputTokens,
                embeddingApiCalls: embeddingEstimate.EmbeddingApiCalls,
                lines: embeddingEstimate.Lines.Concat(["Sin llamadas Vision (solo texto HTML)."]).ToArray());
        }

        return Combine(embeddingEstimate, EstimateVision(imageCount));
    }

    public TokenUsageEstimate EstimateFirstCommentBulkIndex(
        int ticketsToProcess,
        int averageCommentCharCount,
        bool processImages,
        double averageImagesPerTicket = 0)
    {
        if (ticketsToProcess <= 0)
        {
            return BuildEstimate(lines: ["No hay tickets que procesar."]);
        }

        // Document adds ~80 chars metadata (ticket, title, product labels)
        var avgDocChars = averageCommentCharCount + 120;
        var chunksPerTicket = Math.Max(1, EstimateChunkCount(avgDocChars));
        var tokensPerTicket = EstimateTokensFromText(new string('x', avgDocChars));
        var embeddingCalls = ticketsToProcess * chunksPerTicket;
        var embeddingTokens = ticketsToProcess * tokensPerTicket;

        var lines = new List<string>
        {
            $"Embeddings ({_options.EmbeddingModel}): ~{embeddingCalls:N0} llamada(s), ~{embeddingTokens:N0} tokens de entrada.",
            $"Tickets a procesar: {ticketsToProcess:N0} (media ~{avgDocChars:N0} caracteres/documento)."
        };

        TokenUsageEstimate? vision = null;
        if (processImages && averageImagesPerTicket > 0)
        {
            var totalImages = (int)Math.Ceiling(ticketsToProcess * averageImagesPerTicket);
            vision = EstimateVision(totalImages);
            lines.AddRange(vision.Lines);
        }
        else if (processImages)
        {
            lines.Add("Vision: depende de cuántos tickets tengan imágenes (media desconocida).");
        }
        else
        {
            lines.Add("Sin llamadas Vision.");
        }

        var baseEstimate = BuildEstimate(
            embeddingInputTokens: embeddingTokens,
            embeddingApiCalls: embeddingCalls,
            lines: lines);

        return vision is null ? baseEstimate : Combine(baseEstimate, vision);
    }

    public TokenUsageEstimate EstimateRagContextLoad(string? firstCommentText)
    {
        var embeddingTokens = EstimateTokensFromText(firstCommentText);
        return BuildEstimate(
            embeddingInputTokens: embeddingTokens,
            embeddingApiCalls: 1,
            lines:
            [
                $"Embedding del comentario #1 ({_options.EmbeddingModel}): 1 llamada, ~{embeddingTokens:N0} tokens (ya ejecutado al cargar el contexto)."
            ]);
    }

    public TokenUsageEstimate EstimateRagGptAnswer(string? contextText, string? currentTicketFirstComment = null)
    {
        var contextTokens = string.IsNullOrWhiteSpace(contextText)
            ? _options.RagAskEstimatedContextTokens
            : EstimateTokensFromText(contextText);
        var currentTicketTokens = EstimateTokensFromText(currentTicketFirstComment);
        var questionTokens = EstimateTokensFromText(RagOrchestrator.DefaultGenerationQuestion);
        var chatInput = questionTokens + contextTokens + currentTicketTokens + _options.RagAskEstimatedSystemTokens;
        var chatOutput = _options.ChatEstimatedOutputTokens;

        return BuildEstimate(
            chatInputTokens: chatInput,
            chatOutputTokens: chatOutput,
            chatApiCalls: 1,
            lines:
            [
                $"Chat ({_options.ChatModel}): 1 llamada, ~{chatInput:N0} tokens entrada + ~{chatOutput:N0} salida."
            ]);
    }

    public TokenUsageEstimate Combine(params TokenUsageEstimate[] estimates)
    {
        var items = estimates.Where(e => e is not null).ToArray();
        if (items.Length == 0)
        {
            return BuildEstimate();
        }

        return BuildEstimate(
            embeddingInputTokens: items.Sum(e => e.EmbeddingInputTokens),
            embeddingApiCalls: items.Sum(e => e.EmbeddingApiCalls),
            visionInputTokens: items.Sum(e => e.VisionInputTokens),
            visionOutputTokens: items.Sum(e => e.VisionOutputTokens),
            visionApiCalls: items.Sum(e => e.VisionApiCalls),
            chatInputTokens: items.Sum(e => e.ChatInputTokens),
            chatOutputTokens: items.Sum(e => e.ChatOutputTokens),
            chatApiCalls: items.Sum(e => e.ChatApiCalls),
            lines: items.SelectMany(e => e.Lines).Distinct().ToArray());
    }

    private TokenUsageEstimate EstimateVision(int imageCount)
    {
        if (imageCount <= 0)
        {
            return BuildEstimate();
        }

        var inputPerImage = _options.VisionEstimatedInputTokensPerImage;
        var outputPerImage = _options.VisionEstimatedOutputTokensPerImage;
        var inputTokens = imageCount * inputPerImage;
        var outputTokens = imageCount * outputPerImage;

        return BuildEstimate(
            visionInputTokens: inputTokens,
            visionOutputTokens: outputTokens,
            visionApiCalls: imageCount,
            lines:
            [
                $"Vision ({_options.VisionModel}): ~{imageCount} llamada(s), ~{inputTokens:N0} tokens entrada + ~{outputTokens:N0} salida."
            ]);
    }

    private TokenUsageEstimate BuildEstimate(
        int embeddingInputTokens = 0,
        int embeddingApiCalls = 0,
        int visionInputTokens = 0,
        int visionOutputTokens = 0,
        int visionApiCalls = 0,
        int chatInputTokens = 0,
        int chatOutputTokens = 0,
        int chatApiCalls = 0,
        IReadOnlyList<string>? lines = null)
    {
        var embeddingCost = embeddingInputTokens / 1_000_000m * _options.EmbeddingPricePerMillionTokens;
        var visionInputCost = visionInputTokens / 1_000_000m * _options.VisionInputPricePerMillionTokens;
        var visionOutputCost = visionOutputTokens / 1_000_000m * _options.VisionOutputPricePerMillionTokens;
        var chatInputCost = chatInputTokens / 1_000_000m * _options.ChatInputPricePerMillionTokens;
        var chatOutputCost = chatOutputTokens / 1_000_000m * _options.ChatOutputPricePerMillionTokens;
        var total = embeddingCost + visionInputCost + visionOutputCost + chatInputCost + chatOutputCost;

        return new TokenUsageEstimate
        {
            EmbeddingInputTokens = embeddingInputTokens,
            EmbeddingApiCalls = embeddingApiCalls,
            VisionInputTokens = visionInputTokens,
            VisionOutputTokens = visionOutputTokens,
            VisionApiCalls = visionApiCalls,
            ChatInputTokens = chatInputTokens,
            ChatOutputTokens = chatOutputTokens,
            ChatApiCalls = chatApiCalls,
            EstimatedCostUsd = Math.Round(total, 4),
            Lines = lines ?? []
        };
    }
}
