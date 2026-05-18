using System.Text.Json;
using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

[Authorize(Roles = Roles.SuperUser)]
public class UsersController : Controller
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

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

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var document = new BitStoreExportDocument
        {
            ExportedUtc = DateTime.UtcNow,
            Users = await _db.Users
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .Select(user => new BitStoreExportUser
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    PasswordHash = user.PasswordHash,
                    Role = user.Role,
                    CreatedUtc = user.CreatedUtc,
                    Buckets = user.Buckets
                        .OrderBy(bucket => bucket.Id)
                        .Select(bucket => new BitStoreExportBucket
                        {
                            Id = bucket.Id,
                            Name = bucket.Name,
                            Description = bucket.Description,
                            Slug = bucket.Slug,
                            WriteApiKey = bucket.WriteApiKey,
                            CreatedUtc = bucket.CreatedUtc,
                            UpdatedUtc = bucket.UpdatedUtc,
                            Records = bucket.Records
                                .OrderBy(record => record.Id)
                                .Select(record => new BitStoreExportBucketRecord
                                {
                                    Id = record.Id,
                                    Value = record.Value,
                                    CreatedUtc = record.CreatedUtc,
                                    UpdatedUtc = record.UpdatedUtc
                                })
                                .ToList()
                        })
                        .ToList()
                })
                .ToListAsync(cancellationToken)
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, ExportJsonOptions);
        var fileName = $"bitstore-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";

        return File(bytes, "application/json", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(UsersViewModel model, CancellationToken cancellationToken)
    {
        if (model.ImportFile is null || model.ImportFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Choose a BitStore export file to import.";
            return RedirectToAction(nameof(Index));
        }

        BitStoreExportDocument? document;
        await using (var stream = model.ImportFile.OpenReadStream())
        {
            document = await JsonSerializer.DeserializeAsync<BitStoreExportDocument>(
                stream,
                ExportJsonOptions,
                cancellationToken);
        }

        if (document is null || document.SchemaVersion != 1)
        {
            TempData["ErrorMessage"] = "The selected file is not a supported BitStore export.";
            return RedirectToAction(nameof(Index));
        }

        NormalizeExportDocument(document);
        if (!IsValidImportDocument(document, out var importError))
        {
            TempData["ErrorMessage"] = importError;
            return RedirectToAction(nameof(Index));
        }

        var hasExistingData = await _db.Users.AnyAsync(cancellationToken)
            || await _db.Buckets.AnyAsync(cancellationToken)
            || await _db.BucketRecords.AnyAsync(cancellationToken);
        if (hasExistingData && !model.ReplaceExistingData)
        {
            TempData["ErrorMessage"] = "Import would replace existing users and buckets. Check replace existing data to continue.";
            return RedirectToAction(nameof(Index));
        }

        var importedUsers = new List<AppUser>();
        var importedBuckets = new List<Bucket>();
        var importedRecords = new List<BucketRecord>();

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            if (hasExistingData)
            {
                _db.BucketRecords.RemoveRange(_db.BucketRecords);
                _db.Buckets.RemoveRange(_db.Buckets);
                _db.Users.RemoveRange(_db.Users);
                await _db.SaveChangesAsync(cancellationToken);
            }

            importedUsers.Clear();
            foreach (var exportedUser in document.Users)
            {
                importedUsers.Add(new AppUser
                {
                    Id = exportedUser.Id,
                    UserName = exportedUser.UserName.Trim(),
                    PasswordHash = exportedUser.PasswordHash,
                    Role = string.IsNullOrWhiteSpace(exportedUser.Role) ? Roles.User : exportedUser.Role,
                    CreatedUtc = exportedUser.CreatedUtc
                });
            }

            _db.Users.AddRange(importedUsers);
            await _db.SaveChangesAsync(cancellationToken);

            var exportedBuckets = new List<BitStoreExportBucket>();
            importedBuckets.Clear();
            for (var userIndex = 0; userIndex < document.Users.Count; userIndex++)
            {
                var owner = importedUsers[userIndex];
                foreach (var exportedBucket in document.Users[userIndex].Buckets)
                {
                    exportedBuckets.Add(exportedBucket);
                    importedBuckets.Add(new Bucket
                    {
                        Id = exportedBucket.Id,
                        OwnerUserId = owner.Id,
                        Name = exportedBucket.Name,
                        Description = exportedBucket.Description,
                        Slug = exportedBucket.Slug,
                        WriteApiKey = exportedBucket.WriteApiKey,
                        CreatedUtc = exportedBucket.CreatedUtc,
                        UpdatedUtc = exportedBucket.UpdatedUtc
                    });
                }
            }

            _db.Buckets.AddRange(importedBuckets);
            await _db.SaveChangesAsync(cancellationToken);

            importedRecords.Clear();
            for (var bucketIndex = 0; bucketIndex < importedBuckets.Count; bucketIndex++)
            {
                var exportedBucket = exportedBuckets[bucketIndex];
                var importedBucket = importedBuckets[bucketIndex];
                importedRecords.AddRange(exportedBucket.Records.Select(exportedRecord => new BucketRecord
                {
                    Id = exportedRecord.Id,
                    BucketId = importedBucket.Id,
                    Value = exportedRecord.Value,
                    CreatedUtc = exportedRecord.CreatedUtc,
                    UpdatedUtc = exportedRecord.UpdatedUtc
                }));
            }

            _db.BucketRecords.AddRange(importedRecords);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        TempData["StatusMessage"] = $"Imported {importedUsers.Count} users, {importedBuckets.Count} buckets, and {importedRecords.Count} records.";
        return RedirectToAction(nameof(Index));
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

    private static void NormalizeExportDocument(BitStoreExportDocument document)
    {
        document.Users ??= new List<BitStoreExportUser>();
        foreach (var user in document.Users)
        {
            user.Buckets ??= new List<BitStoreExportBucket>();
            foreach (var bucket in user.Buckets)
            {
                bucket.Records ??= new List<BitStoreExportBucketRecord>();
            }
        }
    }

    private static bool IsValidImportDocument(BitStoreExportDocument document, out string errorMessage)
    {
        if (document.Users.Count == 0)
        {
            errorMessage = "The import file does not contain any users.";
            return false;
        }

        if (!document.Users.Any(x => string.Equals(x.Role, Roles.SuperUser, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = "The import file must contain at least one SuperUser account.";
            return false;
        }

        if (document.Users.Any(x => string.IsNullOrWhiteSpace(x.UserName) || string.IsNullOrWhiteSpace(x.PasswordHash)))
        {
            errorMessage = "The import file contains a user without a username or password hash.";
            return false;
        }

        var duplicateUserName = document.Users
            .GroupBy(x => x.UserName.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateUserName is not null)
        {
            errorMessage = $"The import file contains duplicate username '{duplicateUserName.Key}'.";
            return false;
        }

        var duplicateSlug = document.Users
            .SelectMany(x => x.Buckets)
            .Where(x => !string.IsNullOrWhiteSpace(x.Slug))
            .GroupBy(x => x.Slug.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(x => x.Count() > 1);
        if (duplicateSlug is not null)
        {
            errorMessage = $"The import file contains duplicate bucket slug '{duplicateSlug.Key}'.";
            return false;
        }

        if (document.Users.SelectMany(x => x.Buckets).Any(x =>
                string.IsNullOrWhiteSpace(x.Name)
                || string.IsNullOrWhiteSpace(x.Slug)
                || string.IsNullOrWhiteSpace(x.WriteApiKey)))
        {
            errorMessage = "The import file contains a bucket without a name, slug, or write API key.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
