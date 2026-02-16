using BitStoreWeb.Net9.Models;

namespace BitStoreWeb.Net9.Services;

public interface IUserRegistrationNotifier
{
    Task NotifyUserRegisteredAsync(AppUser user);
}
