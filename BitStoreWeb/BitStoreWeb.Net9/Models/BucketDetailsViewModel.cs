using System.ComponentModel.DataAnnotations;

namespace BitStoreWeb.Net9.Models;

public class BucketDetailsViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string WriteApiKey { get; init; } = string.Empty;

    [Required]
    [StringLength(2048)]
    [Display(Name = "Value")]
    public string NewRecordValue { get; set; } = string.Empty;

    public IReadOnlyList<BucketRecordRowViewModel> Records { get; init; } = Array.Empty<BucketRecordRowViewModel>();
}

public class BucketRecordRowViewModel
{
    public int Id { get; init; }

    public string? Value { get; init; }

    public DateTime CreatedUtc { get; init; }

    public DateTime UpdatedUtc { get; init; }
}
