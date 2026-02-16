using BitStoreWeb.Net9.Models;

namespace BitStoreWeb.Net9.Services;

public interface IUserAuthService
{
    Task<LoginResult> LoginAsync(string userName, string password);

    Task<LoginResult> RegisterAsync(string userName, string password);
}
