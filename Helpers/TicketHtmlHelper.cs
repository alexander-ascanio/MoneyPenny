namespace MoneyPenny.Helpers;

using System.Text.RegularExpressions;

public static class TicketHtmlHelper
{
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

        return html.Trim();
    }

    private static bool LooksHtmlEncoded(string content) =>
        content.Contains("&lt;", StringComparison.OrdinalIgnoreCase)
        || content.Contains("&gt;", StringComparison.OrdinalIgnoreCase);
}
