using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

[ApiController]
[Authorize]
[Route("api/buckets")]
public class BucketApiController : ControllerBase
{
    private readonly AppDbContext _db;

    public BucketApiController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBucket(string slug, CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .AsNoTracking()
            .Where(x => x.Slug.ToUpper() == normalizedSlug)
            .Select(x => new BucketSnapshot(
                x.Id,
                x.Name,
                x.Description,
                x.Slug,
                x.UpdatedUtc,
                x.Records.Count()))
            .SingleOrDefaultAsync(cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        var latestRecord = await _db.BucketRecords
            .AsNoTracking()
            .Where(x => x.BucketId == bucket.Id)
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new BucketRecordResponse(x.Id, x.Value, x.CreatedUtc, x.UpdatedUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            bucket = MapBucket(bucket),
            latestRecord
        });
    }

    [HttpGet("{slug}/latest")]
    public async Task<IActionResult> GetLatestRecord(string slug, CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .AsNoTracking()
            .Where(x => x.Slug.ToUpper() == normalizedSlug)
            .Select(x => new BucketSnapshot(
                x.Id,
                x.Name,
                x.Description,
                x.Slug,
                x.UpdatedUtc,
                x.Records.Count()))
            .SingleOrDefaultAsync(cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        var latestRecord = await _db.BucketRecords
            .AsNoTracking()
            .Where(x => x.BucketId == bucket.Id)
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .Select(x => new BucketRecordResponse(x.Id, x.Value, x.CreatedUtc, x.UpdatedUtc))
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new
        {
            bucket = MapBucket(bucket),
            record = latestRecord
        });
    }

    [HttpGet("{slug}/records")]
    public async Task<IActionResult> ListRecords(string slug, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .AsNoTracking()
            .Where(x => x.Slug.ToUpper() == normalizedSlug)
            .Select(x => new BucketSnapshot(
                x.Id,
                x.Name,
                x.Description,
                x.Slug,
                x.UpdatedUtc,
                x.Records.Count()))
            .SingleOrDefaultAsync(cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        var safeTake = Math.Clamp(take, 1, 200);
        var records = await _db.BucketRecords
            .AsNoTracking()
            .Where(x => x.BucketId == bucket.Id)
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .Take(safeTake)
            .Select(x => new BucketRecordResponse(x.Id, x.Value, x.CreatedUtc, x.UpdatedUtc))
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            bucket = MapBucket(bucket),
            count = records.Count,
            records
        });
    }

    [HttpGet("{slug}/records/{recordId:int}")]
    public async Task<IActionResult> GetRecord(string slug, int recordId, CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucketId = await _db.Buckets
            .AsNoTracking()
            .Where(x => x.Slug.ToUpper() == normalizedSlug)
            .Select(x => x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (bucketId == 0)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        var record = await _db.BucketRecords
            .AsNoTracking()
            .Where(x => x.BucketId == bucketId && x.Id == recordId)
            .Select(x => new BucketRecordResponse(x.Id, x.Value, x.CreatedUtc, x.UpdatedUtc))
            .SingleOrDefaultAsync(cancellationToken);
        if (record is null)
        {
            return NotFound(new { message = "Record not found." });
        }

        return Ok(record);
    }

    [HttpPost("{slug}/records")]
    public async Task<IActionResult> AddRecord(
        string slug,
        [FromBody] UpsertBucketRecordRequest request,
        [FromHeader(Name = "X-BitStore-Key")] string? apiKeyHeader,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedValue = request.Value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return BadRequest(new { message = "Value is required." });
        }

        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .SingleOrDefaultAsync(x => x.Slug.ToUpper() == normalizedSlug, cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        if (!HasWriteAccess(bucket, apiKeyHeader, apiKey, out var writeAccessError))
        {
            return Unauthorized(new { message = writeAccessError });
        }

        var record = new BucketRecord
        {
            BucketId = bucket.Id,
            Value = normalizedValue,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _db.BucketRecords.Add(record);
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetRecord),
            new { slug = bucket.Slug, recordId = record.Id },
            MapRecord(record));
    }

    [HttpPut("{slug}/records/{recordId:int}")]
    public async Task<IActionResult> UpdateRecord(
        string slug,
        int recordId,
        [FromBody] UpsertBucketRecordRequest request,
        [FromHeader(Name = "X-BitStore-Key")] string? apiKeyHeader,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedValue = request.Value?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return BadRequest(new { message = "Value is required." });
        }

        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .SingleOrDefaultAsync(x => x.Slug.ToUpper() == normalizedSlug, cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        if (!HasWriteAccess(bucket, apiKeyHeader, apiKey, out var writeAccessError))
        {
            return Unauthorized(new { message = writeAccessError });
        }

        var record = await _db.BucketRecords
            .SingleOrDefaultAsync(x => x.BucketId == bucket.Id && x.Id == recordId, cancellationToken);
        if (record is null)
        {
            return NotFound(new { message = "Record not found." });
        }

        record.Value = normalizedValue;
        record.UpdatedUtc = DateTime.UtcNow;
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapRecord(record));
    }

    [HttpPost("{slug}/records/{recordId:int}/clear")]
    public async Task<IActionResult> ClearRecord(
        string slug,
        int recordId,
        [FromHeader(Name = "X-BitStore-Key")] string? apiKeyHeader,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .SingleOrDefaultAsync(x => x.Slug.ToUpper() == normalizedSlug, cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        if (!HasWriteAccess(bucket, apiKeyHeader, apiKey, out var writeAccessError))
        {
            return Unauthorized(new { message = writeAccessError });
        }

        var record = await _db.BucketRecords
            .SingleOrDefaultAsync(x => x.BucketId == bucket.Id && x.Id == recordId, cancellationToken);
        if (record is null)
        {
            return NotFound(new { message = "Record not found." });
        }

        record.Value = null;
        record.UpdatedUtc = DateTime.UtcNow;
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(MapRecord(record));
    }

    [HttpDelete("{slug}/records/{recordId:int}")]
    public async Task<IActionResult> DeleteRecord(
        string slug,
        int recordId,
        [FromHeader(Name = "X-BitStore-Key")] string? apiKeyHeader,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .SingleOrDefaultAsync(x => x.Slug.ToUpper() == normalizedSlug, cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        if (!HasWriteAccess(bucket, apiKeyHeader, apiKey, out var writeAccessError))
        {
            return Unauthorized(new { message = writeAccessError });
        }

        var record = await _db.BucketRecords
            .SingleOrDefaultAsync(x => x.BucketId == bucket.Id && x.Id == recordId, cancellationToken);
        if (record is null)
        {
            return NotFound(new { message = "Record not found." });
        }

        _db.BucketRecords.Remove(record);
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{slug}/records")]
    public async Task<IActionResult> ClearAllRecords(
        string slug,
        [FromHeader(Name = "X-BitStore-Key")] string? apiKeyHeader,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .SingleOrDefaultAsync(x => x.Slug.ToUpper() == normalizedSlug, cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        if (!HasWriteAccess(bucket, apiKeyHeader, apiKey, out var writeAccessError))
        {
            return Unauthorized(new { message = writeAccessError });
        }

        var records = await _db.BucketRecords
            .Where(x => x.BucketId == bucket.Id)
            .ToListAsync(cancellationToken);

        _db.BucketRecords.RemoveRange(records);
        bucket.UpdatedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpDelete("{slug}")]
    public async Task<IActionResult> DeleteBucket(
        string slug,
        [FromHeader(Name = "X-BitStore-Key")] string? apiKeyHeader,
        [FromQuery] string? apiKey,
        CancellationToken cancellationToken)
    {
        var normalizedSlug = NormalizeLookup(slug);
        var bucket = await _db.Buckets
            .SingleOrDefaultAsync(x => x.Slug.ToUpper() == normalizedSlug, cancellationToken);
        if (bucket is null)
        {
            return NotFound(new { message = "Bucket not found." });
        }

        if (!HasWriteAccess(bucket, apiKeyHeader, apiKey, out var writeAccessError))
        {
            return Unauthorized(new { message = writeAccessError });
        }

        _db.Buckets.Remove(bucket);
        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private bool HasWriteAccess(Bucket bucket, string? apiKeyHeader, string? apiKey, out string errorMessage)
    {
        var providedApiKey = !string.IsNullOrWhiteSpace(apiKeyHeader)
            ? apiKeyHeader.Trim()
            : apiKey;
        if (!string.IsNullOrWhiteSpace(providedApiKey))
        {
            if (string.Equals(bucket.WriteApiKey, providedApiKey.Trim(), StringComparison.Ordinal))
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "Invalid write key for this bucket.";
            return false;
        }

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var userId) && userId == bucket.OwnerUserId)
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = "Write access requires X-BitStore-Key or owner session.";
        return false;
    }

    private static BucketRecordResponse MapRecord(BucketRecord record)
        => new(record.Id, record.Value, record.CreatedUtc, record.UpdatedUtc);

    private static string NormalizeLookup(string value)
        => value.Trim().ToUpperInvariant();

    private static object MapBucket(BucketSnapshot bucket)
        => new
        {
            bucket.Id,
            bucket.Name,
            bucket.Description,
            bucket.Slug,
            bucket.RecordCount,
            bucket.UpdatedUtc
        };

    private sealed record BucketSnapshot(
        int Id,
        string Name,
        string? Description,
        string Slug,
        DateTime UpdatedUtc,
        int RecordCount);

    private sealed record BucketRecordResponse(
        int Id,
        string? Value,
        DateTime CreatedUtc,
        DateTime UpdatedUtc);

    public sealed class UpsertBucketRecordRequest
    {
        [Required]
        [StringLength(8, ErrorMessage = "Value must be 8 characters or fewer.")]
        public string Value { get; set; } = string.Empty;
    }
}
