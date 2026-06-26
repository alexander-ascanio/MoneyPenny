namespace MoneyPenny.Helpers;

using System.Text.RegularExpressions;

public static class TicketHtmlHelper
{
    private static readonly string[] NoiseLinePrefixes =
    [
        "Ticket created via",
        "CAUTION: This email originated from outside of the organization"
    ];

    private static readonly string[] SignatureClosingPrefixes =
    [
        "Saludos,",
        "Saludos cordiales",
        "Saludos.",
        "Saludos",
        "Un saludo,",
        "Un saludo",
        "Atentamente,",
        "Atentamente",
        "Cordialmente,",
        "Cordialmente",
        "Best regards,",
        "Best regards",
        "Kind regards,",
        "Kind regards"
    ];

    private static readonly string[] PrivacyDisclaimerPrefixes =
    [
        "La información incluida en el presente correo",
        "La información contenida en este correo",
        "La información contenida en el presente",
        "La información contenida en este mensaje",
        "Este mensaje y sus adjuntos",
        "Este correo electrónico y cualquier",
        "Este correo electrónico, junto",
        "Este correo electrónico contiene",
        "AVISO LEGAL",
        "CONFIDENCIALIDAD",
        "Asimismo, le informamos de que trataremos",
        "Asimismo, le informamos de que",
        "De conformidad con la Ley Orgánica",
        "De conformidad con la ley orgánica",
        "En cumplimiento de lo dispuesto en",
        "En cumplimiento de la normativa",
        "Le informamos que los datos personales",
        "Sus datos personales serán tratados"
    ];

    private static readonly string[] PrivacyTextAnchors =
    [
        "La información incluida en el presente correo electrónico",
        "La información contenida en este mensaje de correo",
        "están dirigidos exclusivamente a su destinatario",
        "trataremos de forma confidencial su dirección de correo",
        "derechos de acceso, rectificación, cancelación y oposición",
        "protección de datos de carácter personal"
    ];

    private static readonly string[] SignatureTextAnchors =
    [
        "\nSaludos,",
        "\nSaludos\n",
        "\nSaludos cordiales",
        "\nAtentamente,",
        "\nAtentamente\n",
        "\nUn saludo,",
        "\nCordialmente,",
        " Saludos,"
    ];

    private static readonly Regex ContactSignatureLineRegex = new(
        @"^(Tel[eé]fono|Telefono|Fax|Tlf\.?|Móvil|Mobil|Phone)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StandaloneEmailLineRegex = new(
        @"^[\w.+-]+@[\w.-]+\.\w+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string PrepareCommentHtml(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var html = content.Trim();
        if (LooksHtmlEncoded(html))
        {
            html = System.Net.WebUtility.HtmlDecode(html);
        }

        return html;
    }

    public static string ToPlainText(string? content)
    {
        var html = PrepareCommentHtml(content);
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</p>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</div>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<[^>]+>", " ");
        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"[ \t]+", " ");
        html = Regex.Replace(html, @"\n\s*\n+", "\n\n");

        return SanitizeForIndexing(html.Trim());
    }

    public static IReadOnlyList<string> ExtractImageSources(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var html = PrepareCommentHtml(content);
        var matches = ImageSourceRegex.Matches(html);
        var sources = new List<string>();

        foreach (Match match in matches)
        {
            var source = match.Groups[1].Success ? match.Groups[1].Value
                : match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Value;
            source = SanitizeImageSource(source);
            if (!string.IsNullOrWhiteSpace(source))
            {
                sources.Add(source);
            }
        }

        return sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string SanitizeImageSource(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var cleaned = System.Net.WebUtility.HtmlDecode(source).Trim().Trim('"', '\'');
        cleaned = cleaned.Replace("&quot;", string.Empty, StringComparison.OrdinalIgnoreCase);
        return cleaned.Trim();
    }

    private static string TruncateHtmlBeforeFooter(string html)
    {
        var footerIndex = FindHtmlFooterStartIndex(html);
        return footerIndex.HasValue ? html[..footerIndex.Value] : html;
    }

    private static int? FindHtmlFooterStartIndex(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        const int minBodyChars = 15;
        int? earliest = null;

        foreach (var anchor in PrivacyDisclaimerPrefixes.Concat(PrivacyTextAnchors).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var index = html.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (index >= minBodyChars)
            {
                earliest = earliest.HasValue ? Math.Min(earliest.Value, index) : index;
            }
        }

        foreach (var marker in HtmlFooterStartMarkers)
        {
            var index = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= minBodyChars)
            {
                earliest = earliest.HasValue ? Math.Min(earliest.Value, index) : index;
            }
        }

        return earliest;
    }

    private static readonly string[] HtmlFooterStartMarkers =
    [
        ">Saludos,",
        ">Saludos<",
        ">Saludos cordiales",
        ">Atentamente,",
        ">Atentamente<",
        ">Cordialmente,",
        ">Un saludo,",
        ">Un saludo<",
        ">Best regards,",
        ">Kind regards,",
        "<br>Saludos,",
        "<br/>Saludos,",
        "<br />Saludos,",
        "<br>Atentamente,",
        "<br/>Atentamente,",
        "<br />Atentamente,",
        "<br>Cordialmente,",
        "<br/>Cordialmente,",
        "<br />Cordialmente,",
        ">Teléfono:",
        ">Telefono:",
        ">Fax:",
        ">Tlf.:",
        ">Móvil:",
        ">Mobil:",
        "\nSaludos,",
        "\r\nSaludos,",
        " Saludos,",
        "\nAtentamente,",
        "\r\nAtentamente,",
        "\nCordialmente,",
        "\r\nCordialmente,",
        "\nUn saludo,",
        "\r\nUn saludo,"
    ];

    private static readonly Regex ImageSourceRegex = new(
        @"<img\b[^>]*\bsrc\s*=\s*(?:""([^""]+)""|'([^']+)'|([^\s>]+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string SanitizeForIndexing(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = RemoveNoiseLines(text);
        text = RemoveEmailFooter(text);
        return text.Trim();
    }

    public static string RemoveNoiseLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var filtered = text
            .Split('\n')
            .Where(line => !IsNoiseLine(line.Trim()))
            .ToArray();

        return string.Join('\n', filtered).Trim();
    }

    public static string RemoveEmailFooter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = TruncateAtFooterLine(text);
        text = TruncateAtTextAnchor(text, PrivacyTextAnchors);
        text = TruncateAtTextAnchor(text, SignatureTextAnchors);
        return text.Trim();
    }

    private static string TruncateAtFooterLine(string text)
    {
        var lines = text.Split('\n');
        var substantiveLinesBefore = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (IsFooterStartLine(trimmed, substantiveLinesBefore))
            {
                return string.Join('\n', lines.Take(i)).TrimEnd();
            }

            substantiveLinesBefore++;
        }

        return text;
    }

    private static bool IsFooterStartLine(string line, int substantiveLinesBefore)
    {
        if (IsPrivacyDisclaimerStart(line))
        {
            return true;
        }

        if (substantiveLinesBefore == 0)
        {
            return false;
        }

        if (IsSignatureClosingLine(line))
        {
            return true;
        }

        if (IsContactSignatureLine(line))
        {
            return true;
        }

        if (IsLongPrivacyDisclaimerLine(line))
        {
            return true;
        }

        return false;
    }

    private static bool IsSignatureClosingLine(string line) =>
        SignatureClosingPrefixes.Any(prefix =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsPrivacyDisclaimerStart(string line) =>
        PrivacyDisclaimerPrefixes.Any(prefix =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsContactSignatureLine(string line) =>
        ContactSignatureLineRegex.IsMatch(line)
        || StandaloneEmailLineRegex.IsMatch(line);

    private static bool IsLongPrivacyDisclaimerLine(string line)
    {
        if (line.Length < 120)
        {
            return false;
        }

        var matches = PrivacyTextAnchors.Count(anchor =>
            line.Contains(anchor, StringComparison.OrdinalIgnoreCase));

        return matches >= 1;
    }

    private static string TruncateAtTextAnchor(string text, string[] anchors)
    {
        int? cutIndex = null;

        foreach (var anchor in anchors)
        {
            var index = text.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            if (index <= 0)
            {
                continue;
            }

            cutIndex = cutIndex.HasValue ? Math.Min(cutIndex.Value, index) : index;
        }

        return cutIndex.HasValue ? text[..cutIndex.Value].TrimEnd() : text;
    }

    private static bool IsNoiseLine(string line) =>
        NoiseLinePrefixes.Any(prefix =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool LooksHtmlEncoded(string content) =>
        content.Contains("&lt;", StringComparison.OrdinalIgnoreCase)
        || content.Contains("&gt;", StringComparison.OrdinalIgnoreCase);
}
