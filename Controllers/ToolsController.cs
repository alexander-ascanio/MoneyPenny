using Microsoft.AspNetCore.Mvc;

namespace MoneyPenny.Controllers;

public class ToolsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
