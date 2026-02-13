using System.ComponentModel.DataAnnotations;

namespace BitStoreWeb.Net9.Models;

public class BucketRecord
{
    public int Id { get; set; }

    public int BucketId { get; set; }

    public Bucket Bucket { get; set; } = null!;

    [MaxLength(2048)]
    public string? Value { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
