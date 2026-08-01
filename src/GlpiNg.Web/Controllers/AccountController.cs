using System.Security.Claims;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Connexion/déconnexion par cookie d'authentification. Reçoit les soumissions du
/// formulaire HTML classique de <see cref="Components.Pages.Login"/> plutôt que d'être
/// appelé depuis le circuit Blazor : c'est le seul moyen d'invoquer HttpContext.
/// SignInAsync/SignOutAsync avant que les en-têtes de la réponse ne soient envoyés.
/// </summary>
[AllowAnonymous]
[Route("Account")]
public class AccountController(GlpiNgDbContext db, AppSettingsFileStore settingsStore) : Controller
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    [HttpPost("Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [FromForm] string userName,
        [FromForm] string password,
        [FromForm] bool rememberMe,
        [FromForm] string? returnUrl,
        CancellationToken ct)
    {
        GlpiUser? user = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName, ct);

        if (user is null || Hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            return Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.UserName),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
        ];

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        AuthenticationProperties authProperties = new() { IsPersistent = rememberMe };
        if (rememberMe)
        {
            GeneralSettings general = await settingsStore.ReadGeneralAsync(ct);
            if (general.RememberMeDuration > 0)
            {
                authProperties.ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(general.RememberMeDuration);
            }
        }

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), authProperties);

        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }

    [HttpPost("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }
}
