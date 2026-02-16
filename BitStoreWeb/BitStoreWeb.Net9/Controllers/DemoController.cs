using Microsoft.AspNetCore.Mvc;

namespace BitStoreWeb.Net9.Controllers;

public class DemoController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
