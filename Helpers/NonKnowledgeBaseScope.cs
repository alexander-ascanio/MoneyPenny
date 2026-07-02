using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Helpers;

public static class NonKnowledgeBaseScope
{
    public const string SqlFilter = """
         AND t."IsKnowledgeBase" = false
         AND t."GroupName" <> 'Telematel – Knowledge Base'
        """;

    public static IQueryable<Ticket> Apply(IQueryable<Ticket> query) =>
        query.Where(t =>
            !t.IsKnowledgeBase
            && t.Group != TicketListScope.ExcludedGroup);

    public static bool Matches(Ticket ticket) =>
        !ticket.IsKnowledgeBase
        && ticket.Group != TicketListScope.ExcludedGroup;
}
