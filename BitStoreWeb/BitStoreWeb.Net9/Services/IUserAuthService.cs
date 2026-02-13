using BitStoreWeb.Net9.Models;

namespace BitStoreWeb.Net9.Services;

public interface IUserAuthService
{
    Task<LoginResult> LoginOrBootstrapAsync(string userName, string password);
}
