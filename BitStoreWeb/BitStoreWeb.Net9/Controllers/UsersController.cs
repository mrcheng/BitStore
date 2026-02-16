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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(user.Role, Roles.SuperUser, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "SuperUser accounts cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"User '{user.UserName}' was deleted.";
        return RedirectToAction(nameof(Index));
    }
}
