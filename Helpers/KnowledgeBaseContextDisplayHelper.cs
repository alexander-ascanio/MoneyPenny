using System.Text;

namespace MoneyPenny.Helpers;

public static class KnowledgeBaseContextDisplayHelper
{
    private const string TitlePrefix = "Título:";

    private static readonly string[] ProblemMarkers =
    [
        "Pregunta o problema del usuario:",
        "Pregunta o problema del usuario"
    ];

    private static readonly string[] ProblemParagraphStopMarkers =
    [
        "Imagen (opcional):",
        "Imagen (opcional)",
        "Imagen:",
        "Causa:",
        "Causa",
        "Descripción:",
        "Descripcion:",
        "Solución:",
        "Solucion:",
        "SOLUCIÓN:",
        "SOLUCION:",
        "Validación:",
        "Validacion:",
        "VALIDACIÓN:",
        "VALIDACION:",
        "Solución",
        "Solucion",
        "Validación",
        "Validacion"
    ];

    private static readonly string[] LeadingUserLabelLines =
    [
        "del usuario :",
        "del usuario:",
        "del usuario"
    ];

    public static string? Format(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var text = content.Trim();
        var titleLine = ExtractTitleLine(text);
        var problemParagraph = ExtractProblemParagraph(text);

        if (titleLine is null && problemParagraph is null)
        {
            return null;
        }

        var result = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(titleLine))
        {
            result.AppendLine(titleLine);
        }

        if (!string.IsNullOrWhiteSpace(problemParagraph))
        {
            if (result.Length > 0)
            {
                result.AppendLine();
            }

            result.Append(problemParagraph.Trim());
        }

        return result.ToString().Trim();
    }

    private static string? ExtractTitleLine(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static string? ExtractProblemParagraph(string text)
    {
        var markerIndex = FindMarkerIndex(text, ProblemMarkers);
        if (markerIndex < 0)
        {
            return null;
        }

        var marker = GetMatchedMarker(text, markerIndex, ProblemMarkers);
        if (marker is null)
        {
            return null;
        }

        var afterMarker = text[(markerIndex + marker.Length)..].TrimStart();
        afterMarker = afterMarker.TrimStart(':').TrimStart();
        afterMarker = StripLeadingUserLabelLines(afterMarker);

        if (string.IsNullOrWhiteSpace(afterMarker))
        {
            return null;
        }

        var stopIndex = FindLineStartMarkerIndex(afterMarker, ProblemParagraphStopMarkers);
        var paragraph = stopIndex > 0
            ? afterMarker[..stopIndex].TrimEnd()
            : afterMarker.TrimEnd();

        paragraph = TakeFirstParagraph(paragraph);

        return string.IsNullOrWhiteSpace(paragraph) ? null : paragraph;
    }

    private static string StripLeadingUserLabelLines(string text)
    {
        var lines = text.Split('\n');
        var start = 0;

        while (start < lines.Length)
        {
            var trimmed = lines[start].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                start++;
                continue;
            }

            if (LeadingUserLabelLines.Any(label =>
                    trimmed.Equals(label, StringComparison.OrdinalIgnoreCase)))
            {
                start++;
                continue;
            }

            break;
        }

        return string.Join('\n', lines.Skip(start)).TrimStart();
    }

    private static string TakeFirstParagraph(string text)
    {
        var doubleNewlineIndex = text.IndexOf("\n\n", StringComparison.Ordinal);
        if (doubleNewlineIndex > 0)
        {
            return text[..doubleNewlineIndex].TrimEnd();
        }

        var lines = text.Split('\n');
        var paragraphLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (paragraphLines.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (paragraphLines.Count > 0
                && ProblemParagraphStopMarkers.Any(marker =>
                    trimmed.Equals(marker.TrimEnd(':'), StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith(marker, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            paragraphLines.Add(line);
        }

        return string.Join('\n', paragraphLines).TrimEnd();
    }

    private static int FindMarkerIndex(string text, IReadOnlyList<string> markers)
    {
        var lineStartIndex = FindLineStartMarkerIndex(text, markers);
        if (lineStartIndex >= 0)
        {
            return lineStartIndex;
        }

        foreach (var marker in markers.OrderByDescending(m => m.Length))
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

    private static string? GetMatchedMarker(string text, int markerIndex, IReadOnlyList<string> markers)
    {
        foreach (var marker in markers.OrderByDescending(m => m.Length))
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
