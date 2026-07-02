using System.Text;
using MoneyPenny.Data.Repositories;
using MoneyPenny.Helpers;
using MoneyPenny.ViewModels.Rag;

namespace MoneyPenny.Services.Rag;

public static class SimilarTicketThreadContextBuilder
{
    public static async Task<string> BuildAsync(
        IReadOnlyList<RagContextItemViewModel> contextItems,
        ITicketRepository ticketRepository,
        CancellationToken cancellationToken = default)
    {
        if (contextItems.Count == 0)
        {
            return string.Empty;
        }

        var sections = new List<string>(contextItems.Count);
        foreach (var item in contextItems)
        {
            var section = await BuildTicketSectionAsync(item, ticketRepository, cancellationToken);
            if (!string.IsNullOrWhiteSpace(section))
            {
                sections.Add(section);
            }
        }

        return string.Join("\n---\n", sections);
    }

    private static async Task<string> BuildTicketSectionAsync(
        RagContextItemViewModel item,
        ITicketRepository ticketRepository,
        CancellationToken cancellationToken)
    {
        var firstComment = await ticketRepository.GetOldestActionWithContentByTicketIdAsync(
            item.TicketId,
            cancellationToken);

        var actions = await ticketRepository.GetActionsByTicketIdAsync(item.TicketId, cancellationToken);
        var orderedWithContent = actions
            .Where(action => !string.IsNullOrWhiteSpace(action.Content))
            .OrderBy(action => action.CreatedAt)
            .ToList();

        var followUpComments = firstComment is null
            ? orderedWithContent
            : orderedWithContent.Where(action => action.Id != firstComment.Id).ToList();

        var section = new StringBuilder();
        section.AppendLine($"Ticket #{item.TicketNumber} (similitud {item.Score:P0}):");

        if (followUpComments.Count == 0)
        {
            section.AppendLine("(Sin comentarios posteriores al #1 del cliente)");
            return section.ToString().TrimEnd();
        }

        section.AppendLine("Comentarios posteriores al #1 del cliente (incluye respuestas del agente y seguimiento):");

        var commentNumber = 2;
        foreach (var action in followUpComments)
        {
            var plainText = TicketHtmlHelper.ToPlainText(action.Content);
            if (string.IsNullOrWhiteSpace(plainText))
            {
                continue;
            }

            var author = action.CreatedByName
                ?? action.ModifierName
                ?? action.AssignedUsername
                ?? "Desconocido";

            section.AppendLine();
            section.AppendLine(
                $"Comentario #{commentNumber} — {author} ({action.CreatedAt:yyyy-MM-dd HH:mm} UTC):");
            section.AppendLine(plainText.Trim());
            commentNumber++;
        }

        return section.ToString().TrimEnd();
    }
}
