using Microsoft.AspNetCore.Mvc;

namespace BitStoreWeb.Net9.Controllers;

public class BucketController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
