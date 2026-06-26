namespace MoneyPenny.Services.Rag.Ingestion;

public static class IndexedCommentTextHelper
{
    private const string ClientFirstCommentMarker = "Comentario del cliente:";
    private const string TicketDocumentMarker = "Primer comentario:";

    public static string? ExtractFromClientFirstCommentIndex(IEnumerable<string> chunkContents) =>
        ExtractSection(string.Join("\n", chunkContents), ClientFirstCommentMarker);

    public static string? ExtractFromTicketDocumentIndex(IEnumerable<string> chunkContents)
    {
        var full = string.Join("\n", chunkContents);
        var markerIndex = full.IndexOf(TicketDocumentMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var section = full[(markerIndex + TicketDocumentMarker.Length)..].TrimStart();
        var lines = section.Split('\n');
        var contentLines = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Autor:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Fecha:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            contentLines.Add(line);
        }

        var text = string.Join('\n', contentLines).Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string? ExtractSection(string fullText, string marker)
    {
        var markerIndex = fullText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var text = fullText[(markerIndex + marker.Length)..].Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
