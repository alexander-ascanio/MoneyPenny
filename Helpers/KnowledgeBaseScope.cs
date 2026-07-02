using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Helpers;

public static class KnowledgeBaseScope
{
    public const string SqlFilter = """
         AND (
             t."IsKnowledgeBase" = true
             OR t."CustomerName" = 'TELEMATEL INTERNO'
             OR t."GroupName" = 'Telematel – Knowledge Base'
         )
        """;

    public static IQueryable<Ticket> Apply(IQueryable<Ticket> query) =>
        query.Where(t =>
            t.IsKnowledgeBase
            || t.Customer == TicketListScope.ExcludedCustomer
            || t.Group == TicketListScope.ExcludedGroup);

    public static bool Matches(Ticket ticket) =>
        ticket.IsKnowledgeBase
        || ticket.Customer == TicketListScope.ExcludedCustomer
        || ticket.Group == TicketListScope.ExcludedGroup;
}
