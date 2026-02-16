using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Services;

public class UserAuthService : IUserAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public UserAuthService(AppDbContext db, IPasswordHasher<AppUser> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResult> LoginAsync(string userName, string password)
    {
        var normalizedUserName = userName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult
            {
                Succeeded = false,
                ErrorMessage = "Email and password are required."
            };
        }

        var existingUser = await FindByUserNameAsync(normalizedUserName);
        if (existingUser is null)
        {
            return new LoginResult
            {
                Succeeded = false,
                ErrorMessage = "Invalid email or password."
            };
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return new LoginResult
            {
                Succeeded = false,
                ErrorMessage = "Invalid email or password."
            };
        }

        var hasChanges = false;
        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            existingUser.PasswordHash = _passwordHasher.HashPassword(existingUser, password);
            hasChanges = true;
        }

        var firstUserId = await _db.Users
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .FirstAsync();
        if (existingUser.Id == firstUserId && existingUser.Role != Roles.SuperUser)
        {
            existingUser.Role = Roles.SuperUser;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _db.SaveChangesAsync();
        }

        return new LoginResult
        {
            Succeeded = true,
            User = existingUser
        };
    }

    public async Task<LoginResult> RegisterAsync(string userName, string password)
    {
        var normalizedUserName = userName.Trim();

        if (string.IsNullOrWhiteSpace(normalizedUserName) || string.IsNullOrWhiteSpace(password))
        {
            return new LoginResult
            {
                Succeeded = false,
                ErrorMessage = "Email and password are required."
            };
        }

        var existingUser = await FindByUserNameAsync(normalizedUserName);
        if (existingUser is not null)
        {
            return new LoginResult
            {
                Succeeded = false,
                ErrorMessage = "Unable to create account with the provided credentials."
            };
        }

        var hasUsers = await _db.Users.AnyAsync();

        var firstUser = new AppUser
        {
            UserName = normalizedUserName,
            Role = hasUsers ? Roles.User : Roles.SuperUser,
            CreatedUtc = DateTime.UtcNow
        };
        firstUser.PasswordHash = _passwordHasher.HashPassword(firstUser, password);

        _db.Users.Add(firstUser);
        await _db.SaveChangesAsync();

        return new LoginResult
        {
            Succeeded = true,
            User = firstUser
        };
    }

    private async Task<AppUser?> FindByUserNameAsync(string userName)
    {
        var normalizedLookup = userName.ToUpperInvariant();
        return await _db.Users
            .SingleOrDefaultAsync(x => x.UserName.ToUpper() == normalizedLookup);
    }
}
