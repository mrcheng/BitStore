using System.Security.Claims;
using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

[Authorize]
public class BucketController : Controller
{
    private const int MaxRecordValueLength = 8;
    private readonly AppDbContext _db;

    public BucketController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        var model = await BuildViewModelAsync(bucket, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRecord(int id, string value, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "Value is required.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > MaxRecordValueLength)
        {
            TempData["ErrorMessage"] = $"Value must be {MaxRecordValueLength} characters or fewer.";
            return RedirectToAction(nameof(Index), new { id });
        }

        _db.BucketRecords.Add(new BucketRecord
        {
            BucketId = bucket.Id,
            Value = normalizedValue,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        });

        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Record added.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRecord(int id, int recordId, string value, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            TempData["ErrorMessage"] = "Value is required.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var normalizedValue = value.Trim();
        if (normalizedValue.Length > MaxRecordValueLength)
        {
            TempData["ErrorMessage"] = $"Value must be {MaxRecordValueLength} characters or fewer.";
            return RedirectToAction(nameof(Index), new { id });
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        var record = await _db.BucketRecords
            .SingleOrDefaultAsync(x => x.Id == recordId && x.BucketId == bucket.Id, cancellationToken);
        if (record is null)
        {
            TempData["ErrorMessage"] = "Record not found.";
            return RedirectToAction(nameof(Index), new { id });
        }

        record.Value = normalizedValue;
        record.UpdatedUtc = DateTime.UtcNow;
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Record updated.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearRecord(int id, int recordId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        var record = await _db.BucketRecords
            .SingleOrDefaultAsync(x => x.Id == recordId && x.BucketId == bucket.Id, cancellationToken);
        if (record is null)
        {
            TempData["ErrorMessage"] = "Record not found.";
            return RedirectToAction(nameof(Index), new { id });
        }

        record.Value = null;
        record.UpdatedUtc = DateTime.UtcNow;
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Record value cleared.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecord(int id, int recordId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        var record = await _db.BucketRecords
            .SingleOrDefaultAsync(x => x.Id == recordId && x.BucketId == bucket.Id, cancellationToken);
        if (record is null)
        {
            TempData["ErrorMessage"] = "Record not found.";
            return RedirectToAction(nameof(Index), new { id });
        }

        _db.BucketRecords.Remove(record);
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "Record deleted.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAll(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        var records = await _db.BucketRecords
            .Where(x => x.BucketId == bucket.Id)
            .ToListAsync(cancellationToken);

        _db.BucketRecords.RemoveRange(records);
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = "All records removed.";
        return RedirectToAction(nameof(Index), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBucket(int id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var bucket = await GetOwnedBucketAsync(id, userId.Value, cancellationToken);
        if (bucket is null)
        {
            TempData["ErrorMessage"] = "Bucket not found.";
            return RedirectToAction("Index", "Buckets");
        }

        _db.Buckets.Remove(bucket);
        await _db.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Bucket '{bucket.Name}' deleted.";
        return RedirectToAction("Index", "Buckets");
    }

    private async Task<Bucket?> GetOwnedBucketAsync(int bucketId, int userId, CancellationToken cancellationToken)
    {
        return await _db.Buckets
            .SingleOrDefaultAsync(x => x.Id == bucketId && x.OwnerUserId == userId, cancellationToken);
    }

    private async Task<BucketDetailsViewModel> BuildViewModelAsync(Bucket bucket, CancellationToken cancellationToken)
    {
        var records = await _db.BucketRecords
            .AsNoTracking()
            .Where(x => x.BucketId == bucket.Id)
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new BucketRecordRowViewModel
            {
                Id = x.Id,
                Value = x.Value,
                CreatedUtc = x.CreatedUtc,
                UpdatedUtc = x.UpdatedUtc
            })
            .ToListAsync(cancellationToken);

        return new BucketDetailsViewModel
        {
            Id = bucket.Id,
            Name = bucket.Name,
            Description = bucket.Description,
            Slug = bucket.Slug,
            WriteApiKey = bucket.WriteApiKey,
            Records = records
        };
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim, out var userId)
            ? userId
            : null;
    }

}
