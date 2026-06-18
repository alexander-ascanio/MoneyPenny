namespace MoneyPenny.Helpers;

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

    private static bool LooksHtmlEncoded(string content) =>
        content.Contains("&lt;", StringComparison.OrdinalIgnoreCase)
        || content.Contains("&gt;", StringComparison.OrdinalIgnoreCase);
}
