using Microsoft.AspNetCore.Mvc;

namespace BitStoreWeb.Net9.Controllers;

public class BucketsController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
