namespace MoneyPenny.Models;

public class UserManagementViewModel
{
    public ApplicationUser User { get; set; } = null!;
    public List<string> Roles { get; set; } = new();
}
