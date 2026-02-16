using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

[Authorize(Roles = Roles.SuperUser)]
public class UsersController : Controller
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .ThenBy(x => x.UserName)
            .Select(x => new UsersViewModel.UserSummaryViewModel
            {
                Id = x.Id,
                UserName = x.UserName,
                Role = x.Role,
                CreatedUtc = x.CreatedUtc,
                BucketCount = x.Buckets.Count()
            })
            .ToListAsync(cancellationToken);

        var model = new UsersViewModel
        {
            TotalUsers = users.Count,
            Users = users
        };

        return View(model);
    }
}
