namespace BitStoreWeb.Net9.Models;

public class BitStoreExportDocument
{
    public int SchemaVersion { get; set; } = 1;

    public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;

    public List<BitStoreExportUser> Users { get; set; } = new();
}

public class BitStoreExportUser
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.User;

    public DateTime CreatedUtc { get; set; }

    public List<BitStoreExportBucket> Buckets { get; set; } = new();
}

public class BitStoreExportBucket
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string WriteApiKey { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    public List<BitStoreExportBucketRecord> Records { get; set; } = new();
}

public class BitStoreExportBucketRecord
{
    public int Id { get; set; }

    public string? Value { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
