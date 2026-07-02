namespace MoneyPenny.Helpers;

public static class KnowledgeBaseSolutionExtractor
{
    private const string ClientCommentMarker = "Comentario del cliente:";

    private static readonly string[] SolutionMarkers =
    [
        "Solución:",
        "Solucion:",
        "SOLUCIÓN:",
        "SOLUCION:",
        "Solución",
        "Solucion"
    ];

    private static readonly string[] FollowingSectionMarkers =
    [
        "Validación:",
        "Validacion:",
        "VALIDACIÓN:",
        "VALIDACION:",
        "Validación",
        "Validacion"
    ];

    public static string? Extract(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var text = StripIndexedDocumentHeader(content.Trim());
        var markerIndex = FindSolutionMarkerIndex(text);
        if (markerIndex < 0)
        {
            return null;
        }

        var marker = GetMatchedMarker(text, markerIndex);
        if (marker is null)
        {
            return null;
        }

        var solution = text[(markerIndex + marker.Length)..].Trim();
        solution = solution.TrimStart(':').Trim();
        solution = TrimBeforeFollowingSections(solution);

        return string.IsNullOrWhiteSpace(solution) ? null : solution;
    }

    private static string TrimBeforeFollowingSections(string text)
    {
        var sectionIndex = FindLineStartMarkerIndex(text, FollowingSectionMarkers);
        if (sectionIndex <= 0)
        {
            return text;
        }

        return text[..sectionIndex].TrimEnd();
    }

    private static string StripIndexedDocumentHeader(string content)
    {
        var markerIndex = content.IndexOf(ClientCommentMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return content;
        }

        return content[(markerIndex + ClientCommentMarker.Length)..].Trim();
    }

    private static int FindSolutionMarkerIndex(string text)
    {
        var lineStartIndex = FindLineStartMarkerIndex(text, SolutionMarkers);
        if (lineStartIndex >= 0)
        {
            return lineStartIndex;
        }

        foreach (var marker in SolutionMarkers.OrderByDescending(m => m.Length))
        {
            var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindLineStartMarkerIndex(string text, IReadOnlyList<string> markers)
    {
        for (var lineStart = 0; lineStart < text.Length;)
        {
            foreach (var marker in markers.OrderByDescending(m => m.Length))
            {
                if (lineStart + marker.Length <= text.Length
                    && text.AsSpan(lineStart, marker.Length)
                        .Equals(marker.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    var nextCharIndex = lineStart + marker.Length;
                    if (nextCharIndex >= text.Length
                        || text[nextCharIndex] == ':'
                        || char.IsWhiteSpace(text[nextCharIndex]))
                    {
                        return lineStart;
                    }
                }
            }

            var nextLine = text.IndexOf('\n', lineStart);
            if (nextLine < 0)
            {
                break;
            }

            lineStart = nextLine + 1;
            while (lineStart < text.Length && char.IsWhiteSpace(text[lineStart]) && text[lineStart] != '\n')
            {
                lineStart++;
            }
        }

        return -1;
    }

    private static string? GetMatchedMarker(string text, int markerIndex)
    {
        foreach (var marker in SolutionMarkers.OrderByDescending(m => m.Length))
        {
            if (markerIndex + marker.Length <= text.Length
                && text.AsSpan(markerIndex, marker.Length)
                    .Equals(marker.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return marker;
            }
        }

        return null;
    }
}
