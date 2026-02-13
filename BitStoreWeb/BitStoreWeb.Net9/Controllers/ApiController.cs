using Microsoft.AspNetCore.Mvc;

namespace BitStoreWeb.Net9.Controllers;

public class ApiController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
