using Microsoft.AspNetCore.Mvc;

namespace BitStoreWeb.Net9.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
