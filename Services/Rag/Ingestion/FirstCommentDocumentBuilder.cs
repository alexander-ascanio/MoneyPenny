using System.Text;
using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Services.Rag.Ingestion;

public static class FirstCommentDocumentBuilder
{
    public static string Build(TicketFirstCommentRow row, string commentText)
    {
        if (string.IsNullOrWhiteSpace(commentText))
        {
            return string.Empty;
        }

        var document = new StringBuilder();
        document.AppendLine($"Ticket: {row.TicketNumber}");

        if (!string.IsNullOrWhiteSpace(row.Title))
        {
            document.AppendLine($"Título: {row.Title}");
        }

        if (!string.IsNullOrWhiteSpace(row.Product))
        {
            document.AppendLine($"Producto: {row.Product}");
        }

        document.AppendLine("Comentario del cliente:");
        document.AppendLine(commentText);
        return document.ToString().Trim();
    }
}
