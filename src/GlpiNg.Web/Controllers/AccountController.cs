using System.Security.Claims;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Connexion/déconnexion par cookie d'authentification. Reçoit les soumissions du
/// formulaire HTML classique de <see cref="Components.Pages.Login"/> plutôt que d'être
/// appelé depuis le circuit Blazor : c'est le seul moyen d'invoquer HttpContext.
/// SignInAsync/SignOutAsync avant que les en-têtes de la réponse ne soient envoyés.
/// </summary>
[AllowAnonymous]
[Route("Account")]
public class AccountController(UserCredentialAuthenticator credentialAuthenticator, SettingsCacheService settingsStore) : Controller
{
    [HttpPost("Login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        [FromForm] string userName,
        [FromForm] string password,
        [FromForm] bool rememberMe,
        [FromForm] string? returnUrl,
        // Valeur du sélecteur de source de connexion (voir Login.razor, affiché seulement si
        // GeneralSettings.ShowAuthSourcesOnLoginPage) : null = pas de sélecteur affiché, choix
        // automatique par UserCredentialAuthenticator ; 0 = "Base GlpiNg (local)" imposé
        // explicitement ; N = AuthLdapServer.Id imposé explicitement.
        [FromForm] int? authSource,
        CancellationToken ct)
    {
        GlpiUser? user = await credentialAuthenticator.AuthenticateAsync(userName, password, authSource, ct);

        if (user is null)
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
            GeneralSettings general = await settingsStore.ReadSectionAsync<GeneralSettings>("GeneralSettings", ct);
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
