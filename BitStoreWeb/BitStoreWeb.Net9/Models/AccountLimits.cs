namespace BitStoreWeb.Net9.Models;

public static class AccountLimits
{
    public const int FreeBucketLimit = 1;

    public const int FreeRecordLimit = 100;

    public static bool IsFreeAccount(string? role)
        => string.Equals(role, Roles.User, StringComparison.OrdinalIgnoreCase);
}
