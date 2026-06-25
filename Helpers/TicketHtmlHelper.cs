namespace MoneyPenny.Helpers;

using System.Text.RegularExpressions;

public static class TicketHtmlHelper
{
    private static readonly string[] NoiseLinePrefixes =
    [
        "Ticket created via",
        "CAUTION: This email originated from outside of the organization"
    ];

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

        return RemoveNoiseLines(html.Trim());
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

    private static bool IsNoiseLine(string line) =>
        NoiseLinePrefixes.Any(prefix =>
            line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool LooksHtmlEncoded(string content) =>
        content.Contains("&lt;", StringComparison.OrdinalIgnoreCase)
        || content.Contains("&gt;", StringComparison.OrdinalIgnoreCase);
}
