using System.Security.Claims;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;

namespace GlpiNg.Web.Services;

/// <summary>
/// Lit les droits du contexte courant dans les revendications déposées à la connexion. Aucune
/// requête base : les droits sont consultés à chaque rendu de menu, à chaque requête filtrée par
/// le middleware et à chaque écriture.
///
/// Reprend exactement la logique de résolution d'utilisateur d'<see cref="EntityScopeProvider"/>,
/// y compris ses cas neutres : hors session applicative (protocole agent, cron) et pour un appel
/// machine-à-machine par jeton OAuth, on rend <see cref="ProfileRights.Full"/> — sinon l'agent ne
/// pourrait plus écrire les inventaires et l'import ne pourrait plus rien créer.
/// </summary>
public sealed class ProfileRightsProvider(
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services) : IProfileRightsProvider
{
    private ProfileRights? _current;

    public ProfileRights Current => _current ??= Resolve();

    private ProfileRights Resolve()
    {
        // Appel d'API : droits du profil actif de la session (voir EntityScopeProvider).
        if (httpContextAccessor.HttpContext?.Items[Api.GlpiApiAccess.RightsItemKey] is ProfileRights apiRights)
        {
            return apiRights;
        }

        ClaimsPrincipal? user = GetUser();

        if (user?.Identity?.IsAuthenticated != true)
        {
            return ProfileRights.Full;
        }

        // Le marqueur de session applicative sert de discriminant ici aussi : une identité
        // authentifiée qui ne le porte pas est un jeton OAuth, pas un utilisateur de l'UI.
        if (user.FindFirst(UserEntityAccessService.UnrestrictedClaim) is null)
        {
            return ProfileRights.Full;
        }

        if (user.HasClaim(UserEntityAccessService.UnrestrictedClaim, "1"))
        {
            return ProfileRights.Full;
        }

        return new ProfileRights(section =>
            int.TryParse(user.FindFirst(ProfileRightsService.ClaimTypeFor(section))?.Value, out int level)
            && Enum.IsDefined((ProfileRightLevel)level)
                ? (ProfileRightLevel)level
                : ProfileRightLevel.None);
    }

    /// <summary>Voir <see cref="EntityScopeProvider.GetUser"/> : mêmes contraintes, même solution.</summary>
    private ClaimsPrincipal? GetUser()
    {
        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            return httpContext.User;
        }

        try
        {
            Task<AuthenticationState>? stateTask = services
                .GetService<AuthenticationStateProvider>()
                ?.GetAuthenticationStateAsync();

            return stateTask?.IsCompletedSuccessfully == true ? stateTask.Result.User : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
