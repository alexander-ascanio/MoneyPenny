namespace MoneyPenny.Helpers;

using System.Text.RegularExpressions;

public static class TicketHtmlHelper
{
    private static readonly string[] NoiseLinePrefixes =
    [
        "Ticket created via",
        "CAUTION: This email originated from outside of the organization",
        "ADVERTENCIA: Este correo electr\u00f3nico procede de fuera de la organizaci\u00f3n",
        "ADVERTENCIA: Este correo procede de fuera de la organizaci\u00f3n",
        "These people were on the To line of the email",
        "These people were on the CC line of the email"
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
        "La informaci\u00f3n incluida en el presente correo",
        "La información contenida en este correo",
        "La informaci\u00f3n contenida en este correo",
        "La información contenida en el presente",
        "La informaci\u00f3n contenida en el presente",
        "La información contenida en este mensaje",
        "La informaci\u00f3n contenida en este mensaje",
        "Este mensaje y sus adjuntos",
        "Este correo electr\u00f3nico y cualquier",
        "Este correo electr\u00f3nico, junto",
        "Este correo electr\u00f3nico contiene",
        "AVISO LEGAL",
        "CONFIDENCIALIDAD",
        "Asimismo, le informamos de que trataremos",
        "Asimismo, le informamos de que",
        "De conformidad con la Ley Org\u00e1nica",
        "De conformidad con la ley org\u00e1nica",
        "En cumplimiento de lo dispuesto en",
        "En cumplimiento de la normativa",
        "Le informamos que los datos personales",
        "Sus datos personales ser\u00e1n tratados",
        "Antes de imprimir este mensaje piense bien"
    ];

    private static readonly string[] PrivacyTextAnchors =
    [
        "La información incluida en el presente correo electr\u00f3nico",
        "La informaci\u00f3n incluida en el presente correo electr\u00f3nico",
        "La información contenida en este mensaje de correo",
        "La informaci\u00f3n contenida en este mensaje de correo",
        "est\u00e1n dirigidos exclusivamente a su destinatario",
        "están dirigidos exclusivamente a su destinatario",
        "trataremos de forma confidencial su direcci\u00f3n de correo",
        "trataremos de forma confidencial su dirección de correo",
        "derechos de acceso, rectificaci\u00f3n, cancelaci\u00f3n y oposici\u00f3n",
        "derechos de acceso, rectificación, cancelación y oposición",
        "protecci\u00f3n de datos de car\u00e1cter personal",
        "protección de datos de carácter personal",
        "mensaje y/o archivo(s) adjunto(s), enviada desde"
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
        @"^(Tel[eé]fono|Telefono|Fax|Tlf\.?|M[óo]vil|Mobil|Phone|Móvil)\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StandaloneEmailLineRegex = new(
        @"^[\w.+-]+@[\w.-]+\.\w+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DepartmentLineRegex = new(
        @"^(Departamento|Dpto\.?|Área|Area|Gerencia|Divisi[oó]n|Secci[oó]n|Unidad)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PostalAddressLineRegex = new(
        @"\b(C\/|Calle|Av\.|Avenida|Pol[ií]gono|P\.?\s*I\.?|Vereda|Carretera|Plaza|Paseo)\b|\bN[ºo°]\s*\d|\b\d{5}\b.*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BarePhoneLineRegex = new(
        @"\b\d{3}[\s.-]?\d{2,3}[\s.-]?\d{2,3}\b(\s*\|\s*\d{3}[\s.-]?\d{2,3}[\s.-]?\d{2,3}\b)?",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmailWebLineRegex = new(
        @"[\w.+-]+@[\w.-]+\.\w+.*\|\s*(www\.|https?://)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PersonNameLineRegex = new(
        @"^[A-ZÁÉÍÓÚÜÑ][a-záéíóúüñ]+(?:\s+[A-ZÁÉÍÓÚÜÑ][a-záéíóúüñ]+){1,3}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

    public static bool ContentMentionsAttachment(string? content)
    {
        var text = ToPlainText(content);
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] markers =
        [
            "adjunto",
            "adjunta",
            "adjunt",
            "captura de pantalla",
            "captura de imagen",
            "screenshot",
            "screen shot",
            "attached image",
            "imagen adjunta",
            "archivo adjunto",
            "anexo"
        ];

        return markers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    public static string PrepareCommentHtmlForDisplay(
        string? content,
        Func<string, string>? attachmentProxyUrlFactory = null)
    {
        var html = PrepareCommentHtml(content);
        if (string.IsNullOrWhiteSpace(html) || attachmentProxyUrlFactory is null)
        {
            return html;
        }

        html = RewriteAttachmentAttributeUrls(html, "src", attachmentProxyUrlFactory);
        html = RewriteAttachmentAttributeUrls(html, "href", attachmentProxyUrlFactory);
        return html;
    }

    public static bool IsLikelyImageAttachmentUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var path = url.Split('?', '#')[0];
        return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/attachments/", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GuessAttachmentFileName(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var lastSegment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrWhiteSpace(lastSegment) ? "Adjunto" : lastSegment;
    }

    public static bool IsGuidLikeAttachmentName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(name.Trim().Trim('"', '\''));
        return GuidAttachmentNameRegex.IsMatch(stem);
    }

    public static bool ShouldDisplayAsCommentAttachment(string url, string? overrideFileName = null)
    {
        if (!string.IsNullOrWhiteSpace(overrideFileName))
        {
            return !IsGuidLikeAttachmentName(overrideFileName);
        }

        return !IsGuidLikeAttachmentName(GuessAttachmentFileName(url));
    }

    private static readonly Regex GuidAttachmentNameRegex = new(
        @"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string RewriteAttachmentAttributeUrls(
        string html,
        string attributeName,
        Func<string, string> attachmentProxyUrlFactory)
    {
        var pattern = $@"(<{attributeName}\s*=\s*)(?:""([^""]+)""|'([^']+)'|([^\s>]+))";
        return Regex.Replace(
            html,
            pattern,
            match =>
            {
                var source = match.Groups[2].Success ? match.Groups[2].Value
                    : match.Groups[3].Success ? match.Groups[3].Value
                    : match.Groups[4].Value;
                source = SanitizeImageSource(source);
                if (!LooksLikeTeamSupportAttachmentUrl(source))
                {
                    return match.Value;
                }

                var proxyUrl = attachmentProxyUrlFactory(source);
                return $"{match.Groups[1].Value}\"{proxyUrl}\"";
            },
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool LooksLikeTeamSupportAttachmentUrl(string source) =>
        source.Contains("teamsupport.com", StringComparison.OrdinalIgnoreCase)
        && source.Contains("/attachments/", StringComparison.OrdinalIgnoreCase);

    public static string PrepareCommentHtmlForIndexing(string? content)
    {
        var html = PrepareCommentHtml(content);
        return string.IsNullOrWhiteSpace(html) ? string.Empty : TruncateHtmlBeforeFooter(html);
    }

    public static string ToPlainText(string? content)
    {
        var html = PrepareCommentHtmlForIndexing(content);
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        html = System.Net.WebUtility.HtmlDecode(html);
        html = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</p>", "\n", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"</div>", "\n", RegexOptions.IgnoreCase);
        html = StripHtmlTags(html);
        html = Regex.Replace(html, @"[ \t]+", " ");
        html = Regex.Replace(html, @"\n\s*\n+", "\n\n");

        return SanitizeForIndexing(html.Trim());
    }

    private static string StripHtmlTags(string html)
    {
        html = Regex.Replace(html, @"<[^>]*>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<[^>]*", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return html;
    }

    public static IReadOnlyList<string> ExtractImageSources(string? content) =>
        ExtractImageSourcesFromHtml(content, PrepareCommentHtmlForIndexing);

    public static IReadOnlyList<string> ExtractCommentImageSources(string? content) =>
        ExtractImageSourcesFromHtml(content, PrepareCommentHtml);

    private static IReadOnlyList<string> ExtractImageSourcesFromHtml(
        string? content,
        Func<string?, string> htmlPreparer)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var html = htmlPreparer(content);
        var matches = ImageSourceRegex.Matches(html);
        var sources = new List<string>();

        foreach (Match match in matches)
        {
            var source = match.Groups[1].Success ? match.Groups[1].Value
                : match.Groups[2].Success ? match.Groups[2].Value
                : match.Groups[3].Value;
            source = SanitizeImageSource(source);
            if (string.IsNullOrWhiteSpace(source) || IsLikelySignatureImageUrl(source))
            {
                continue;
            }

            sources.Add(source);
        }

        return sources.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsLikelySignatureImageUrl(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        string[] markers =
        [
            "logo_firma",
            "logo-firma",
            "/firma.",
            "marcas-firma",
            "email-signature",
            "/signature/",
            "recycle.png",
            "linkedin.com/in/",
            "facebook.com/",
            "twitter.com/",
            "instagram.com/"
        ];

        return markers.Any(marker => source.Contains(marker, StringComparison.OrdinalIgnoreCase));
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
        if (!footerIndex.HasValue)
        {
            return html;
        }

        var truncated = html[..footerIndex.Value];
        return RemoveIncompleteTrailingHtmlTag(truncated);
    }

    private static string RemoveIncompleteTrailingHtmlTag(string html) =>
        Regex.Replace(html, @"<[^>]*\z", string.Empty, RegexOptions.Singleline);

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

        foreach (var marker in HtmlFooterStartMarkers.Concat(HtmlSignatureBlockMarkers))
        {
            var index = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index >= minBodyChars)
            {
                earliest = earliest.HasValue ? Math.Min(earliest.Value, index) : index;
            }
        }

        return earliest;
    }

    private static readonly string[] HtmlSignatureBlockMarkers =
    [
        "logo_firma",
        "logo-firma",
        "/firma.png",
        "/firma.jpg",
        "/firma.gif",
        "marcas-firma/",
        "email-signature",
        "/signature/",
        ">Departamento ",
        ">Departamento&nbsp;",
        ">Dpto.",
        ">Dpto&nbsp;",
        "La informaci\u00f3n contenida en este mensaje",
        "La información contenida en este mensaje",
        "La informaci\u00f3n incluida en el presente correo",
        "La información incluida en el presente correo"
    ];

    private static readonly string[] HtmlFooterStartMarkers =
    [
        ">Saludos,",
        ">Saludos.",
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
        ">Tel?fono:",
        ">Fax:",
        ">Tlf.:",
        ">Móvil:",
        ">Mobil:",
        ">M?vil:",
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
        text = RemoveResidualHtmlLines(text);
        return text.Trim();
    }

    private static string RemoveResidualHtmlLines(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var filtered = text
            .Split('\n')
            .Where(line => !IsResidualHtmlLine(line.Trim()))
            .ToArray();

        return string.Join('\n', filtered);
    }

    private static bool IsResidualHtmlLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith('<'))
        {
            return false;
        }

        return line.Contains("style=", StringComparison.OrdinalIgnoreCase)
            || line.Contains("</", StringComparison.Ordinal)
            || Regex.IsMatch(line, @"^</?\w+", RegexOptions.IgnoreCase);
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

            if (IsLikelySignatureBlockStart(trimmed, lines, i, substantiveLinesBefore))
            {
                return string.Join('\n', lines.Take(i)).TrimEnd();
            }

            substantiveLinesBefore++;
        }

        return text;
    }

    private static bool IsLikelySignatureBlockStart(
        string line,
        string[] lines,
        int lineIndex,
        int substantiveLinesBefore)
    {
        if (IsPrivacyDisclaimerStart(line))
        {
            return substantiveLinesBefore > 0;
        }

        if (substantiveLinesBefore == 0)
        {
            return false;
        }

        if (IsSignatureClosingLine(line)
            || IsContactSignatureLine(line)
            || IsLongPrivacyDisclaimerLine(line))
        {
            return true;
        }

        if (IsPersonNameLine(line) && ScoreSignatureWindow(lines, lineIndex, 8) >= 2)
        {
            return true;
        }

        if (IsDepartmentLine(line) && ScoreSignatureWindow(lines, lineIndex, 8) >= 2)
        {
            return true;
        }

        return ScoreSignatureWindow(lines, lineIndex, 6) >= 3;
    }

    private static int ScoreSignatureWindow(string[] lines, int startIndex, int maxNonEmptyLines)
    {
        var score = 0;
        var nonEmpty = 0;

        for (var i = startIndex; i < lines.Length && nonEmpty < maxNonEmptyLines; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            nonEmpty++;
            if (IsDepartmentLine(trimmed)) score++;
            if (IsPostalAddressLine(trimmed)) score++;
            if (IsBarePhoneLine(trimmed)) score++;
            if (IsEmailWebLine(trimmed)) score++;
            if (IsContactSignatureLine(trimmed)) score++;
            if (IsAllCapsCompanyTagline(trimmed)) score++;
            if (IsPersonNameLine(trimmed)) score++;
            if (IsPrivacyDisclaimerStart(trimmed)) score += 2;
        }

        return score;
    }

    private static bool IsDepartmentLine(string line) => DepartmentLineRegex.IsMatch(line);

    private static bool IsPostalAddressLine(string line) => PostalAddressLineRegex.IsMatch(line);

    private static bool IsBarePhoneLine(string line) =>
        BarePhoneLineRegex.IsMatch(line) && !Regex.IsMatch(line, @"[A-Za-z]{4,}");

    private static bool IsEmailWebLine(string line) => EmailWebLineRegex.IsMatch(line);

    private static bool IsPersonNameLine(string line) =>
        PersonNameLineRegex.IsMatch(line) && line.Length <= 60;

    private static bool IsAllCapsCompanyTagline(string line)
    {
        if (line.Length < 25)
        {
            return false;
        }

        var letters = line.Count(char.IsLetter);
        if (letters < 15)
        {
            return false;
        }

        var uppercase = line.Count(ch => char.IsLetter(ch) && char.IsUpper(ch));
        return uppercase >= letters * 0.8;
    }

    private static bool IsSignatureClosingLine(string line) =>
        SignatureClosingPrefixes.Any(prefix =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsPrivacyDisclaimerStart(string line) =>
        PrivacyDisclaimerPrefixes.Any(prefix =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsContactSignatureLine(string line) =>
        ContactSignatureLineRegex.IsMatch(line)
        || StandaloneEmailLineRegex.IsMatch(line)
        || IsEmailWebLine(line)
        || IsBarePhoneLine(line);

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
