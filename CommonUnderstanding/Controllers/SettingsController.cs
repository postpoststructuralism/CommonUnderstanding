using Microsoft.AspNetCore.Mvc;

namespace CommonUnderstanding.Controllers;

public class SettingsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
