using System.ComponentModel.DataAnnotations;

namespace BitStoreWeb.Net9.Models;

public class Bucket
{
    public int Id { get; set; }

    public int OwnerUserId { get; set; }

    public AppUser OwnerUser { get; set; } = null!;

    [Required]
    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(80)]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string WriteApiKey { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<BucketRecord> Records { get; set; } = new List<BucketRecord>();
}
