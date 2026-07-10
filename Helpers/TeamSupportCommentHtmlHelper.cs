using System.Net;

namespace MoneyPenny.Helpers;

public static class TeamSupportCommentHtmlHelper
{
    public static string ToPrivateCommentHtml(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return string.Empty;
        }

        var normalized = answer.Replace("\r\n", "\n").Trim();
        var paragraphs = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (paragraphs.Length == 0)
        {
            return string.Empty;
        }

        var htmlParagraphs = paragraphs.Select(paragraph =>
        {
            var escaped = WebUtility.HtmlEncode(paragraph);
            var withBreaks = escaped.Replace("\n", "<br />", StringComparison.Ordinal);
            return $"<p>{withBreaks}</p>";
        });

        return string.Concat(htmlParagraphs);
    }
}
