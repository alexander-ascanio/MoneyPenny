using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Helpers;

public static class KnowledgeBaseScope
{
    public const string KnowledgeBaseGroup = TicketListScope.ExcludedGroup;
    public const string KnowledgeBaseCustomer = TicketListScope.ExcludedCustomer;

    public const string SqlFilter = """
         AND t."IsKnowledgeBase" = true
         AND t."GroupName" = 'Telematel – Knowledge Base'
         AND t."CustomerName" = 'TELEMATEL INTERNO'
        """;

    public static IQueryable<Ticket> Apply(IQueryable<Ticket> query) =>
        query.Where(t =>
            t.IsKnowledgeBase
            && t.Group == KnowledgeBaseGroup
            && t.Customer == KnowledgeBaseCustomer);

    public static bool Matches(Ticket ticket) =>
        ticket.IsKnowledgeBase
        && ticket.Group == KnowledgeBaseGroup
        && ticket.Customer == KnowledgeBaseCustomer;
}
