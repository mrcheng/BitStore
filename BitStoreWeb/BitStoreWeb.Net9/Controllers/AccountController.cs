using System.Security.Claims;
using BitStoreWeb.Net9.Data;
using BitStoreWeb.Net9.Models;
using BitStoreWeb.Net9.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BitStoreWeb.Net9.Controllers;

public class AccountController : Controller
{
    private const string LoginMode = "login";
    private const string RegisterMode = "register";

    private readonly IUserAuthService _userAuthService;
    private readonly AppDbContext _db;

    public AccountController(IUserAuthService userAuthService, AppDbContext db)
    {
        _userAuthService = userAuthService;
        _db = db;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null, string? mode = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        var authMode = NormalizeAuthMode(mode);
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["AuthMode"] = authMode;
        ViewData["ShowBootstrapHint"] = !await _db.Users.AnyAsync();
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, string? mode = null)
    {
        var authMode = NormalizeAuthMode(mode);
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["AuthMode"] = authMode;
        ViewData["ShowBootstrapHint"] = !await _db.Users.AnyAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var loginResult = authMode == RegisterMode
            ? await _userAuthService.RegisterAsync(model.UserName, model.Password)
            : await _userAuthService.LoginAsync(model.UserName, model.Password);
        if (!loginResult.Succeeded || loginResult.User is null)
        {
            ModelState.AddModelError(
                string.Empty,
                loginResult.ErrorMessage ?? (authMode == RegisterMode ? "Account creation failed." : "Login failed."));
            return View(model);
        }

        var user = loginResult.User;
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Role, user.Role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(14)
                : DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return RedirectToLocal(returnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private static string NormalizeAuthMode(string? mode)
    {
        return string.Equals(mode, RegisterMode, StringComparison.OrdinalIgnoreCase)
            ? RegisterMode
            : LoginMode;
    }
}
