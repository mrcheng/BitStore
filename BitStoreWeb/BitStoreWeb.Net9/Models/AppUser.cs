namespace BitStoreWeb.Net9.Models;

public class AppUser
{
    public int Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = Roles.User;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Bucket> Buckets { get; set; } = new List<Bucket>();
}
