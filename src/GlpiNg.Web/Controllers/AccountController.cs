using System.Security.Claims;
using GlpiNg.Web.Data;
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
public class AccountController(
    UserCredentialAuthenticator credentialAuthenticator,
    SettingsCacheService settingsStore,
    UserEntityAccessService entityAccess,
    EntityTreeCache entityTree,
    ProfileRightsService profileRights,
    IRootDbContextFactory rootDbFactory,
    EventLogService eventLog) : Controller
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
            await eventLog.LogAsync("login", EventLogLevel.Warning, $"Échec de connexion pour « {userName} ».", cancellationToken: ct);
            return Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
        }

        await eventLog.LogAsync("login", EventLogLevel.Info, "Connexion réussie.",
            itemType: nameof(GlpiUser), itemId: user.Id, itemLabel: user.DisplayName ?? user.UserName, cancellationToken: ct);

        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName ?? user.UserName),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User"),
        ];

        // Cloisonnement par entité : l'entité active de la session est portée par le cookie.
        // Voir UserEntityAccessService — un administrateur reçoit à la place une revendication
        // « non cloisonné », un utilisateur sans habilitation n'en reçoit aucune et ne verra rien.
        (List<Claim> scopeClaims, int? activeEntityId) = await entityAccess.BuildScopeClaimsAsync(user, ct);
        claims.AddRange(scopeClaims);

        // Droits par profil, résolus pour cette entité active : un profil est habilité SUR une
        // entité, donc les droits ne sont connus qu'une fois celle-ci choisie.
        claims.AddRange(await profileRights.BuildRightClaimsAsync(user, activeEntityId, entityTree, ct));

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

    /// <summary>
    /// Change l'entité active de la session, à la façon du sélecteur d'entité de GLPI. Passe par
    /// un POST de formulaire classique et non par le circuit Blazor, pour la même raison que
    /// <see cref="Login"/> : réémettre le cookie exige un HttpContext dont les en-têtes ne sont
    /// pas encore partis. La page est ensuite rechargée, ce qui reconstruit le circuit avec le
    /// nouveau cloisonnement.
    /// </summary>
    [HttpPost("SwitchEntity")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> SwitchEntity(
        [FromForm] int entityId,
        [FromForm] bool recursive,
        [FromForm] string? returnUrl,
        CancellationToken ct)
    {
        if (!int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
        {
            return Redirect("/login");
        }

        await using GlpiNgDbContext db = await rootDbFactory.CreateDbContextAsync(ct);
        GlpiUser? user = await db.Users.FindAsync([userId], ct);

        if (user is null)
        {
            return Redirect("/login");
        }

        // L'entité vient d'un formulaire : elle doit être revalidée contre les habilitations,
        // sinon n'importe qui pourrait s'octroyer la visibilité d'une autre entité en rejouant
        // le POST.
        if (!await entityAccess.CanActivateAsync(user, entityId, entityTree, ct))
        {
            await eventLog.LogAsync("login", EventLogLevel.Warning,
                $"Tentative d'activation d'une entité non habilitée (entité {entityId}).",
                itemType: nameof(GlpiUser), itemId: user.Id, itemLabel: user.DisplayName ?? user.UserName, cancellationToken: ct);

            return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }

        // Identité reconstruite à partir de l'actuelle, revendications de cloisonnement mises à
        // jour : on ne repasse pas par l'authentification, l'utilisateur est déjà connecté.
        List<Claim> claims = [.. User.Claims.Where(c =>
            c.Type != UserEntityAccessService.UnrestrictedClaim
            && c.Type != UserEntityAccessService.ActiveEntityClaim
            && c.Type != UserEntityAccessService.RecursiveClaim
            && !c.Type.StartsWith(ProfileRightsService.RightClaimPrefix, StringComparison.Ordinal))];

        claims.AddRange(user.IsAdmin
            ? [new Claim(UserEntityAccessService.UnrestrictedClaim, "1")]
            : UserEntityAccessService.BuildScopeClaims(entityId, recursive));

        // Les droits sont recalculés, pas repris : ils dépendent de l'entité active, et
        // l'utilisateur peut très bien être gestionnaire ici et simple lecteur là.
        claims.AddRange(await profileRights.BuildRightClaimsAsync(user, entityId, entityTree, ct));

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // Conserve la persistance et l'échéance du cookie courant : basculer d'entité ne doit pas
        // transformer un "Se souvenir de moi" en session éphémère.
        AuthenticationProperties properties = (await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme))
            .Properties ?? new AuthenticationProperties();

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);

        return LocalRedirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }

    [HttpPost("Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        string? userName = User.Identity?.Name;
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrEmpty(userName))
        {
            await eventLog.LogAsync("login", EventLogLevel.Info, "Déconnexion.", itemLabel: userName);
        }

        return Redirect("/login");
    }
}
