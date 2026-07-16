using MoneyPenny.Models.Tickets;

namespace MoneyPenny.Helpers;

public static class TicketListScope
{
    public const string ExcludedCustomer = "TELEMATEL INTERNO";
    public const string UnknownCompanyCustomer = "_Unknown Company";
    public const string ExcludedGroup = "Telematel – Knowledge Base";

    public const string SqlFilter = """
         AND t."IsKnowledgeBase" = false
         AND t."CustomerName" <> 'TELEMATEL INTERNO'
         AND t."GroupName" <> 'Telematel – Knowledge Base'
        """;

    public static IQueryable<Ticket> Apply(IQueryable<Ticket> query) =>
        query.Where(t =>
            !t.IsKnowledgeBase
            && t.Customer != ExcludedCustomer
            && t.Group != ExcludedGroup);

    public static bool Matches(Ticket ticket) =>
        !ticket.IsKnowledgeBase
        && ticket.Customer != ExcludedCustomer
        && ticket.Group != ExcludedGroup;
}
