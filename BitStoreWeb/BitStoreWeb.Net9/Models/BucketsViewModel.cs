using System.ComponentModel.DataAnnotations;

namespace BitStoreWeb.Net9.Models;

public class BucketsViewModel
{
    [Required]
    [StringLength(80)]
    [Display(Name = "Bucket name")]
    public string NewBucketName { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Description")]
    public string? NewBucketDescription { get; set; }

    public IReadOnlyList<BucketSummaryViewModel> Buckets { get; set; } = Array.Empty<BucketSummaryViewModel>();
}

public class BucketSummaryViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string WriteApiKey { get; init; } = string.Empty;

    public int RecordCount { get; init; }

    public DateTime UpdatedUtc { get; init; }
}
