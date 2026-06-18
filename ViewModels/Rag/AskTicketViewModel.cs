using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.ViewModels.Rag;

public class AskTicketViewModel
{
    public int? TicketId { get; set; }
    public string? TicketNumber { get; set; }

    [Required(ErrorMessage = "La pregunta es obligatoria.")]
    [Display(Name = "Pregunta")]
    public string Question { get; set; } = string.Empty;
}
