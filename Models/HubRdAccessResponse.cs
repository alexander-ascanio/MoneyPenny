namespace MoneyPenny.Models;

public class HubRdAccessResponse
{
    public string App { get; set; } = "";
    public string Email { get; set; } = "";
    public bool HasAccess { get; set; }
    public string Reason { get; set; } = "";
    public bool IsAdmin { get; set; }
    public string Role { get; set; } = "";
}
