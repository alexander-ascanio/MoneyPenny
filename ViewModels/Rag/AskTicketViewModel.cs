namespace MoneyPenny.ViewModels.Rag;

public class AskTicketViewModel
{
    public int TicketId { get; set; }
    public string? TicketNumber { get; set; }
    public bool GenerateGptAnswer { get; set; }
}
