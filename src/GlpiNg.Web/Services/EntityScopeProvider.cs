using System.Security.Claims;
using GlpiNg.Modules.Abstractions.Entities;
using Microsoft.AspNetCore.Components.Authorization;

namespace GlpiNg.Web.Services;

/// <summary>
/// Calcule le <see cref="EntityScope"/> du contexte courant à partir des revendications déposées
/// à la connexion (voir <see cref="UserEntityAccessService"/>) et de l'arbre des entités en
/// cache. Aucune requête base n'est nécessaire une fois le cache chaud, ce qui compte : il est
/// consulté à chaque création de contexte EF.
///
/// Enregistré en <c>Scoped</c> et mémorisé pour la durée du scope (circuit Blazor ou requête
/// HTTP) : le cloisonnement ne change pas en cours de session, changer d'entité active réémet le
/// cookie et repart sur un nouveau circuit.
/// </summary>
public sealed class EntityScopeProvider(
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services,
    EntityTreeCache tree) : IEntityScopeProvider
{
    private EntityScope? _current;

    public EntityScope Current => _current ??= Resolve();

    private EntityScope Resolve()
    {
        ClaimsPrincipal? user = GetUser();

        if (user?.Identity?.IsAuthenticated != true)
        {
            // Pas de session applicative : protocole agent (/inventory), tâches cron, appels
            // machine-à-machine par jeton OAuth. Ces chemins doivent voir tout le parc.
            return EntityScope.Unrestricted;
        }

        Claim? marker = user.FindFirst(UserEntityAccessService.UnrestrictedClaim);

        if (marker is null)
        {
            // Identité authentifiée mais pas issue du formulaire de connexion : jeton OAuth d'un
            // appel machine-à-machine (voir GlpiImportController). Il n'y a pas d'utilisateur
            // applicatif derrière, donc rien à cloisonner — sans ça l'import ne verrait plus rien.
            return EntityScope.Unrestricted;
        }

        if (marker.Value == "1")
        {
            return EntityScope.Unrestricted;
        }

        if (!int.TryParse(user.FindFirst(UserEntityAccessService.ActiveEntityClaim)?.Value, out int activeEntityId))
        {
            // Connecté mais sans entité active : utilisateur sans habilitation. Voir
            // UserEntityAccessService.BuildScopeClaimsAsync.
            return EntityScope.None;
        }

        bool recursive = user.HasClaim(UserEntityAccessService.RecursiveClaim, "1");

        int[] visible = recursive ? tree.GetSelfAndDescendants(activeEntityId) : [activeEntityId];

        return EntityScope.Restricted(activeEntityId, visible, tree.GetAncestors(visible), recursive);
    }

    /// <summary>
    /// L'utilisateur courant vient de <see cref="IHttpContextAccessor"/> pendant une vraie requête
    /// HTTP (contrôleurs, pré-rendu), et de l'<see cref="AuthenticationStateProvider"/> une fois
    /// dans le circuit Blazor, où il n'y a plus de HttpContext.
    ///
    /// L'<see cref="AuthenticationStateProvider"/> n'est sollicité qu'en dernier recours, et
    /// résolu paresseusement dans un try/catch : hors du scope DI d'un composant Razor il lève
    /// une <see cref="InvalidOperationException"/> (« Do not call GetAuthenticationStateAsync
    /// outside of the DI scope for a Razor component »). Ce cas se produit pour de bon — scope
    /// d'une tâche cron, outils EF Core au design time — et il n'y a alors pas d'utilisateur,
    /// donc pas de cloisonnement à appliquer.
    ///
    /// L'état d'authentification du circuit est posé à son ouverture, donc la tâche est déjà
    /// terminée : on ne bloque jamais dessus.
    /// </summary>
    private ClaimsPrincipal? GetUser()
    {
        // Une requête HTTP porte toujours son utilisateur (authentifié ou anonyme) : inutile
        // d'aller interroger le circuit, et surtout pas de risque de lever.
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
