namespace BitStoreWeb.Net9.Models;

public class UsersViewModel
{
    public int TotalUsers { get; set; }

    public IReadOnlyList<UserSummaryViewModel> Users { get; set; } = Array.Empty<UserSummaryViewModel>();

    public class UserSummaryViewModel
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Role { get; set; } = Roles.User;

        public DateTime CreatedUtc { get; set; }

        public int BucketCount { get; set; }
    }
}
