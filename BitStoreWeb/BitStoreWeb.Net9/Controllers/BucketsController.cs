using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

[Authorize]
public class BucketsController : Controller
{
    private readonly AppDbContext _db;

    public BucketsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        return View(new BucketsViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Data(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var buckets = await LoadBucketSummariesAsync(userId.Value, cancellationToken);
        var data = buckets.Select(x => new
        {
            x.Id,
            x.Name,
            x.Description,
            x.Slug,
            x.WriteApiKey,
            x.RecordCount,
            x.UpdatedUtc
        });

        return Json(new { data });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BucketsViewModel model, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        model.NewBucketName = model.NewBucketName?.Trim() ?? string.Empty;
        model.NewBucketDescription = string.IsNullOrWhiteSpace(model.NewBucketDescription)
            ? null
            : model.NewBucketDescription.Trim();

        if (!ModelState.IsValid)
        {
            model.Buckets = await LoadBucketSummariesAsync(userId.Value, cancellationToken);
            return View("Index", model);
        }

        var normalizedBucketName = model.NewBucketName.ToUpperInvariant();
        var nameExists = await _db.Buckets.AnyAsync(
            x => x.OwnerUserId == userId.Value && x.Name.ToUpper() == normalizedBucketName,
            cancellationToken);
        if (nameExists)
        {
            ModelState.AddModelError(nameof(model.NewBucketName), "You already have a bucket with this name.");
            model.Buckets = await LoadBucketSummariesAsync(userId.Value, cancellationToken);
            return View("Index", model);
        }

        var bucket = new Bucket
        {
            OwnerUserId = userId.Value,
            Name = model.NewBucketName,
            Description = model.NewBucketDescription,
            Slug = await GenerateUniqueSlugAsync(model.NewBucketName, cancellationToken),
            WriteApiKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant(),
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.Buckets.Add(bucket);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Bucket '{bucket.Name}' was created.";
        return RedirectToAction("Index", "Bucket", new { id = bucket.Id });
    }

    private async Task<List<BucketSummaryViewModel>> LoadBucketSummariesAsync(int userId, CancellationToken cancellationToken)
    {
        return await _db.Buckets
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .OrderBy(x => x.Name)
            .Select(x => new BucketSummaryViewModel
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                Slug = x.Slug,
                WriteApiKey = x.WriteApiKey,
                RecordCount = x.Records.Count(),
                UpdatedUtc = x.UpdatedUtc
            })
            .ToListAsync(cancellationToken);
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId)
            ? userId
            : null;
    }

    private async Task<string> GenerateUniqueSlugAsync(string bucketName, CancellationToken cancellationToken)
    {
        var baseSlug = Slugify(bucketName);
        for (var i = 0; i < 20; i++)
        {
            var suffix = i == 0 ? string.Empty : $"-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLowerInvariant()}";
            var candidate = $"{baseSlug}{suffix}";
            if (candidate.Length > 80)
            {
                candidate = candidate[..80].TrimEnd('-');
            }

            var exists = await _db.Buckets.AnyAsync(x => x.Slug == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        var fallback = $"{baseSlug}-{Guid.NewGuid():N}";
        if (fallback.Length > 80)
        {
            fallback = fallback[..80];
        }

        return fallback.TrimEnd('-');
    }

    private static string Slugify(string input)
    {
        var lower = input.ToLowerInvariant().Trim();
        var slug = Regex.Replace(lower, "[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "bucket";
        }

        return slug.Length > 60
            ? slug[..60].TrimEnd('-')
            : slug;
    }
}
