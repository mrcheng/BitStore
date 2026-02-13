namespace BitStoreWeb.Net9.Models;

public class LoginResult
{
    public required bool Succeeded { get; init; }

    public string? ErrorMessage { get; init; }

    public AppUser? User { get; init; }
}
