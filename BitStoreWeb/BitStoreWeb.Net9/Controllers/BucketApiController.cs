using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

[ApiController]
[Route("api/buckets")]
public class BucketApiController : ControllerBase
{
    private const int MaxValuePatternLength = 128;

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
    public async Task<IActionResult> ListRecords(
        string slug,
        [FromQuery] int take = 50,
        [FromQuery] string? cursor = null,
        [FromQuery] string? date = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] string? week = null,
        [FromQuery] string? month = null,
        [FromQuery] string? timeZone = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] DateTime? beforeUtc = null,
        [FromQuery] DateTime? updatedFromUtc = null,
        [FromQuery] DateTime? updatedToUtc = null,
        [FromQuery] string? value = null,
        [FromQuery] string? valuePrefix = null,
        [FromQuery] string[]? valuePrefixes = null,
        [FromQuery] string? valuePrefixFrom = null,
        [FromQuery] string? valuePrefixTo = null,
        [FromQuery] string? valueContains = null,
        [FromQuery] string? valuePattern = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListBucketRecordsQuery
        {
            Take = take,
            Cursor = cursor,
            Date = date,
            FromDate = fromDate,
            ToDate = toDate,
            Week = week,
            Month = month,
            TimeZone = timeZone,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            BeforeUtc = beforeUtc,
            UpdatedFromUtc = updatedFromUtc,
            UpdatedToUtc = updatedToUtc,
            Value = value,
            ValuePrefix = valuePrefix,
            ValuePrefixes = valuePrefixes,
            ValuePrefixFrom = valuePrefixFrom,
            ValuePrefixTo = valuePrefixTo,
            ValueContains = valueContains,
            ValuePattern = valuePattern
        };

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

        var safeTake = Math.Clamp(query.Take, 1, 200);
        if (!TryDecodeCursor(query.Cursor, out var recordCursor, out var cursorError))
        {
            return BadRequest(new { message = cursorError });
        }

        if (!TryBuildCalendarCreatedUtcRange(query, out var calendarFromUtc, out var calendarToUtc, out var calendarError))
        {
            return BadRequest(new { message = calendarError });
        }

        var recordsQuery = _db.BucketRecords
            .AsNoTracking()
            .Where(x => x.BucketId == bucket.Id);

        if (calendarFromUtc.HasValue)
        {
            recordsQuery = recordsQuery.Where(x => x.CreatedUtc >= calendarFromUtc.Value);
        }

        if (calendarToUtc.HasValue)
        {
            recordsQuery = recordsQuery.Where(x => x.CreatedUtc < calendarToUtc.Value);
        }

        if (query.FromUtc.HasValue)
        {
            var normalizedFromUtc = NormalizeUtc(query.FromUtc.Value);
            recordsQuery = recordsQuery.Where(x => x.CreatedUtc >= normalizedFromUtc);
        }

        if (query.ToUtc.HasValue)
        {
            var normalizedToUtc = NormalizeUtc(query.ToUtc.Value);
            recordsQuery = recordsQuery.Where(x => x.CreatedUtc < normalizedToUtc);
        }

        if (query.BeforeUtc.HasValue)
        {
            var normalizedBeforeUtc = NormalizeUtc(query.BeforeUtc.Value);
            recordsQuery = recordsQuery.Where(x => x.CreatedUtc < normalizedBeforeUtc);
        }

        if (query.UpdatedFromUtc.HasValue)
        {
            var normalizedUpdatedFromUtc = NormalizeUtc(query.UpdatedFromUtc.Value);
            recordsQuery = recordsQuery.Where(x => x.UpdatedUtc >= normalizedUpdatedFromUtc);
        }

        if (query.UpdatedToUtc.HasValue)
        {
            var normalizedUpdatedToUtc = NormalizeUtc(query.UpdatedToUtc.Value);
            recordsQuery = recordsQuery.Where(x => x.UpdatedUtc < normalizedUpdatedToUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Value))
        {
            var normalizedValue = query.Value.Trim();
            recordsQuery = recordsQuery.Where(x => x.Value == normalizedValue);
        }

        var normalizedValuePrefixes = NormalizeValuePrefixes(query.ValuePrefix, query.ValuePrefixes);
        if (normalizedValuePrefixes.Count > 0)
        {
            recordsQuery = recordsQuery.Where(BuildValuePrefixesPredicate(normalizedValuePrefixes));
        }

        if (!string.IsNullOrWhiteSpace(query.ValuePrefixFrom))
        {
            var normalizedValuePrefixFrom = query.ValuePrefixFrom.Trim();
            recordsQuery = recordsQuery.Where(x => x.Value != null && string.Compare(x.Value, normalizedValuePrefixFrom) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(query.ValuePrefixTo))
        {
            var normalizedValuePrefixTo = query.ValuePrefixTo.Trim();
            recordsQuery = recordsQuery.Where(x => x.Value != null && string.Compare(x.Value, normalizedValuePrefixTo) < 0);
        }

        if (!string.IsNullOrWhiteSpace(query.ValueContains))
        {
            var normalizedValueContains = query.ValueContains.Trim();
            recordsQuery = recordsQuery.Where(x => x.Value != null && x.Value.Contains(normalizedValueContains));
        }

        if (!TryNormalizeValuePattern(query.ValuePattern, out var normalizedValuePattern, out var valuePatternError))
        {
            return BadRequest(new { message = valuePatternError });
        }

        if (!string.IsNullOrWhiteSpace(normalizedValuePattern))
        {
            recordsQuery = recordsQuery.Where(x => x.Value != null && Regex.IsMatch(x.Value, normalizedValuePattern));
        }

        if (recordCursor is not null)
        {
            recordsQuery = recordsQuery.Where(x =>
                x.CreatedUtc < recordCursor.CreatedUtc ||
                x.CreatedUtc == recordCursor.CreatedUtc && x.Id < recordCursor.Id);
        }

        var recordsPage = await recordsQuery
            .OrderByDescending(x => x.CreatedUtc)
            .ThenByDescending(x => x.Id)
            .Take(safeTake + 1)
            .Select(x => new BucketRecordResponse(x.Id, x.Value, x.CreatedUtc, x.UpdatedUtc))
            .ToListAsync(cancellationToken);

        var hasMore = recordsPage.Count > safeTake;
        var records = recordsPage.Take(safeTake).ToList();
        var nextCursor = hasMore && records.Count > 0
            ? EncodeCursor(records[^1])
            : null;

        return Ok(new
        {
            bucket = MapBucket(bucket),
            count = records.Count,
            hasMore,
            nextCursor,
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

        if (await FreeRecordLimitReachedAsync(bucket.Id, bucket.OwnerUserId, cancellationToken))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = $"Free accounts can store up to {AccountLimits.FreeRecordLimit} records per bucket." });
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

    private async Task<bool> FreeRecordLimitReachedAsync(int bucketId, int ownerUserId, CancellationToken cancellationToken)
    {
        var userRole = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == ownerUserId)
            .Select(x => x.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (!AccountLimits.IsFreeAccount(userRole))
        {
            return false;
        }

        var recordCount = await _db.BucketRecords
            .CountAsync(x => x.BucketId == bucketId, cancellationToken);
        return recordCount >= AccountLimits.FreeRecordLimit;
    }

    private static BucketRecordResponse MapRecord(BucketRecord record)
        => new(record.Id, record.Value, record.CreatedUtc, record.UpdatedUtc);

    private static string NormalizeLookup(string value)
        => value.Trim().ToUpperInvariant();

    private static DateTime NormalizeUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Utc => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private static bool TryBuildCalendarCreatedUtcRange(
        ListBucketRecordsQuery query,
        out DateTime? fromUtc,
        out DateTime? toUtc,
        out string errorMessage)
    {
        fromUtc = null;
        toUtc = null;
        errorMessage = string.Empty;

        var hasDate = !string.IsNullOrWhiteSpace(query.Date);
        var hasDateRange = !string.IsNullOrWhiteSpace(query.FromDate) || !string.IsNullOrWhiteSpace(query.ToDate);
        var hasWeek = !string.IsNullOrWhiteSpace(query.Week);
        var hasMonth = !string.IsNullOrWhiteSpace(query.Month);
        var calendarFilterCount = Convert.ToInt32(hasDate)
            + Convert.ToInt32(hasDateRange)
            + Convert.ToInt32(hasWeek)
            + Convert.ToInt32(hasMonth);

        if (calendarFilterCount == 0)
        {
            return true;
        }

        if (calendarFilterCount > 1)
        {
            errorMessage = "Use only one calendar filter: date, fromDate/toDate, week, or month.";
            return false;
        }

        if (!TryFindTimeZone(query.TimeZone, out var timeZone, out errorMessage))
        {
            return false;
        }

        if (hasDate)
        {
            if (!TryParseDate(query.Date, "date", out var date, out errorMessage))
            {
                return false;
            }

            if (!TryAddDays(date, 1, "date", out var nextDate, out errorMessage))
            {
                return false;
            }

            return TryConvertLocalRangeToUtc(date, nextDate, timeZone, out fromUtc, out toUtc, out errorMessage);
        }

        if (hasDateRange)
        {
            DateOnly? fromDate = null;
            DateOnly? toDate = null;

            if (!string.IsNullOrWhiteSpace(query.FromDate))
            {
                if (!TryParseDate(query.FromDate, "fromDate", out var parsedFromDate, out errorMessage))
                {
                    return false;
                }

                fromDate = parsedFromDate;
            }

            if (!string.IsNullOrWhiteSpace(query.ToDate))
            {
                if (!TryParseDate(query.ToDate, "toDate", out var parsedToDate, out errorMessage))
                {
                    return false;
                }

                toDate = parsedToDate;
            }

            if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            {
                errorMessage = "fromDate must be before or equal to toDate.";
                return false;
            }

            if (fromDate.HasValue &&
                !TryConvertLocalDateBoundaryToUtc(fromDate.Value, timeZone, out fromUtc, out errorMessage))
            {
                return false;
            }

            if (toDate.HasValue &&
                (!TryAddDays(toDate.Value, 1, "toDate", out var nextToDate, out errorMessage) ||
                 !TryConvertLocalDateBoundaryToUtc(nextToDate, timeZone, out toUtc, out errorMessage)))
            {
                return false;
            }

            return true;
        }

        if (hasWeek)
        {
            if (!TryParseIsoWeek(query.Week, out var weekStart, out errorMessage))
            {
                return false;
            }

            if (!TryAddDays(weekStart, 7, "week", out var weekEnd, out errorMessage))
            {
                return false;
            }

            return TryConvertLocalRangeToUtc(weekStart, weekEnd, timeZone, out fromUtc, out toUtc, out errorMessage);
        }

        if (!TryParseMonth(query.Month, out var monthStart, out errorMessage))
        {
            return false;
        }

        if (!TryAddMonths(monthStart, 1, "month", out var nextMonth, out errorMessage))
        {
            return false;
        }

        return TryConvertLocalRangeToUtc(monthStart, nextMonth, timeZone, out fromUtc, out toUtc, out errorMessage);
    }

    private static bool TryFindTimeZone(string? value, out TimeZoneInfo timeZone, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            timeZone = TimeZoneInfo.Utc;
            return true;
        }

        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(value.Trim());
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        timeZone = TimeZoneInfo.Utc;
        errorMessage = $"Unknown timeZone '{value}'. Use a system time zone ID such as 'UTC' or 'Europe/Stockholm'.";
        return false;
    }

    private static bool TryParseDate(string? value, string parameterName, out DateOnly date, out string errorMessage)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            errorMessage = string.Empty;
            return true;
        }

        errorMessage = $"{parameterName} must use yyyy-MM-dd.";
        return false;
    }

    private static bool TryParseIsoWeek(string? value, out DateOnly weekStart, out string errorMessage)
    {
        weekStart = default;
        var parts = value?.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is null ||
            parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            year is < 1 or > 9999)
        {
            errorMessage = "week must use yyyy-Www, for example 2026-W21.";
            return false;
        }

        var weekPart = parts[1].StartsWith('W') || parts[1].StartsWith('w')
            ? parts[1][1..]
            : parts[1];
        if (!int.TryParse(weekPart, NumberStyles.None, CultureInfo.InvariantCulture, out var week) ||
            week < 1 ||
            week > ISOWeek.GetWeeksInYear(year))
        {
            errorMessage = "week must use a valid ISO week, for example 2026-W21.";
            return false;
        }

        weekStart = DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday));
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryParseMonth(string? value, out DateOnly monthStart, out string errorMessage)
    {
        monthStart = default;
        var parts = value?.Trim().Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not [var yearPart, var monthPart] ||
            !int.TryParse(yearPart, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            year is < 1 or > 9999 ||
            !int.TryParse(monthPart, NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            month is < 1 or > 12)
        {
            errorMessage = "month must use yyyy-MM.";
            return false;
        }

        monthStart = new DateOnly(year, month, 1);
        errorMessage = string.Empty;
        return true;
    }

    private static bool TryAddDays(
        DateOnly date,
        int days,
        string parameterName,
        out DateOnly result,
        out string errorMessage)
    {
        try
        {
            result = date.AddDays(days);
            errorMessage = string.Empty;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            errorMessage = $"{parameterName} is outside the supported date range.";
            return false;
        }
    }

    private static bool TryAddMonths(
        DateOnly date,
        int months,
        string parameterName,
        out DateOnly result,
        out string errorMessage)
    {
        try
        {
            result = date.AddMonths(months);
            errorMessage = string.Empty;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            errorMessage = $"{parameterName} is outside the supported date range.";
            return false;
        }
    }

    private static bool TryConvertLocalRangeToUtc(
        DateOnly startDate,
        DateOnly endDate,
        TimeZoneInfo timeZone,
        out DateTime? fromUtc,
        out DateTime? toUtc,
        out string errorMessage)
    {
        if (!TryConvertLocalDateBoundaryToUtc(startDate, timeZone, out fromUtc, out errorMessage))
        {
            toUtc = null;
            return false;
        }

        return TryConvertLocalDateBoundaryToUtc(endDate, timeZone, out toUtc, out errorMessage);
    }

    private static bool TryConvertLocalDateBoundaryToUtc(
        DateOnly date,
        TimeZoneInfo timeZone,
        out DateTime? utc,
        out string errorMessage)
    {
        var localBoundary = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        try
        {
            utc = TimeZoneInfo.ConvertTimeToUtc(localBoundary, timeZone);
            errorMessage = string.Empty;
            return true;
        }
        catch (ArgumentException)
        {
            utc = null;
            errorMessage = $"The date boundary {date:yyyy-MM-dd} is invalid in timeZone '{timeZone.Id}'.";
            return false;
        }
    }

    private static List<string> NormalizeValuePrefixes(string? valuePrefix, string[]? valuePrefixes)
    {
        var prefixes = new List<string>();
        AddPrefix(valuePrefix, prefixes);

        if (valuePrefixes is not null)
        {
            foreach (var entry in valuePrefixes)
            {
                if (string.IsNullOrWhiteSpace(entry))
                {
                    continue;
                }

                foreach (var prefix in entry.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    AddPrefix(prefix, prefixes);
                }
            }
        }

        return prefixes
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void AddPrefix(string? value, List<string> prefixes)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            prefixes.Add(value.Trim());
        }
    }

    private static bool TryNormalizeValuePattern(string? value, out string? pattern, out string errorMessage)
    {
        pattern = null;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        pattern = value.Trim();
        if (pattern.Length > MaxValuePatternLength)
        {
            errorMessage = $"valuePattern must be {MaxValuePatternLength} characters or fewer.";
            return false;
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            return true;
        }
        catch (ArgumentException)
        {
            errorMessage = "valuePattern must be a valid regular expression.";
            return false;
        }
    }

    private static Expression<Func<BucketRecord, bool>> BuildValuePrefixesPredicate(IReadOnlyList<string> prefixes)
    {
        var record = Expression.Parameter(typeof(BucketRecord), "record");
        var value = Expression.Property(record, nameof(BucketRecord.Value));
        var notNull = Expression.NotEqual(value, Expression.Constant(null, typeof(string)));
        var startsWithMethod = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
        Expression? prefixChecks = null;

        foreach (var prefix in prefixes)
        {
            var startsWith = Expression.Call(value, startsWithMethod, Expression.Constant(prefix));
            prefixChecks = prefixChecks is null
                ? startsWith
                : Expression.OrElse(prefixChecks, startsWith);
        }

        var body = Expression.AndAlso(notNull, prefixChecks ?? Expression.Constant(false));
        return Expression.Lambda<Func<BucketRecord, bool>>(body, record);
    }

    private static string EncodeCursor(BucketRecordResponse record)
    {
        var cursorValue = $"{record.CreatedUtc.Ticks}:{record.Id}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(cursorValue))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeCursor(string? value, out RecordCursor? cursor, out string errorMessage)
    {
        cursor = null;
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var paddedValue = value.Trim()
            .Replace('-', '+')
            .Replace('_', '/');
        paddedValue = paddedValue.PadRight(paddedValue.Length + (4 - paddedValue.Length % 4) % 4, '=');

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(paddedValue));
            var parts = decoded.Split(':', 2);
            if (parts.Length == 2 &&
                long.TryParse(parts[0], out var ticks) &&
                int.TryParse(parts[1], out var id))
            {
                cursor = new RecordCursor(new DateTime(ticks, DateTimeKind.Utc), id);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        errorMessage = "Invalid cursor.";
        return false;
    }

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

    private sealed record RecordCursor(DateTime CreatedUtc, int Id);

    public sealed class ListBucketRecordsQuery
    {
        public int Take { get; set; } = 50;

        public string? Cursor { get; set; }

        public string? Date { get; set; }

        public string? FromDate { get; set; }

        public string? ToDate { get; set; }

        public string? Week { get; set; }

        public string? Month { get; set; }

        public string? TimeZone { get; set; }

        public DateTime? FromUtc { get; set; }

        public DateTime? ToUtc { get; set; }

        public DateTime? BeforeUtc { get; set; }

        public DateTime? UpdatedFromUtc { get; set; }

        public DateTime? UpdatedToUtc { get; set; }

        public string? Value { get; set; }

        public string? ValuePrefix { get; set; }

        public string[]? ValuePrefixes { get; set; }

        public string? ValuePrefixFrom { get; set; }

        public string? ValuePrefixTo { get; set; }

        public string? ValueContains { get; set; }

        public string? ValuePattern { get; set; }
    }

    public sealed class UpsertBucketRecordRequest
    {
        [Required]
        [StringLength(8, ErrorMessage = "Value must be 8 characters or fewer.")]
        public string Value { get; set; } = string.Empty;
    }
}
