using System.ComponentModel.DataAnnotations;

namespace MoneyPenny.Models;

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;

    [Required(ErrorMessage = "El email es requerido")]
    [EmailAddress(ErrorMessage = "Email inválido")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es requerido")]
    [Display(Name = "Nombre")]
    public string DisplayName { get; set; } = string.Empty;

    public List<string> UserRoles { get; set; } = new();
    public List<string> AllRoles { get; set; } = new();
    public List<string> SelectedRoles { get; set; } = new();
}
