using System.Security.Claims;
using GlpiNg.Modules.Abstractions.Preferences;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Lit les préférences d'affichage du compte connecté (voir <see cref="IUserPreferences"/>).
///
/// Enregistré en Scoped, et le résultat est mémorisé : un circuit Blazor lit une fois, pas une par
/// tableau affiché. Hors session applicative — protocole agent, cron, jeton OAuth — on rend les
/// valeurs par défaut plutôt que d'échouer : une préférence d'affichage n'a aucun sens là, et rien
/// ne doit dépendre de sa présence.
///
/// La lecture vise la base et non les revendications de la session, contrairement à
/// <see cref="ProfileRightsProvider"/> : un droit ne change qu'à la connexion suivante, ce qui est
/// acceptable pour une habilitation, alors qu'un réglage que l'utilisateur vient d'enregistrer doit
/// s'appliquer tout de suite.
/// </summary>
public sealed class UserPreferencesProvider(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    SettingsCacheService settings,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services) : IUserPreferences
{
    private UserPreferenceValues? _cached;

    public async ValueTask<UserPreferenceValues> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is { } cached)
        {
            return cached;
        }

        return _cached = await ResolveAsync(cancellationToken);
    }

    private async Task<UserPreferenceValues> ResolveAsync(CancellationToken cancellationToken)
    {
        MacAddressFormat instanceFormat = MacAddressFormatter.FromSettingsValue(
            (await settings.ReadSectionAsync<GeneralSettings>("GeneralSettings", cancellationToken)).MacAddressFormat);

        if (GetUser() is not { Identity.IsAuthenticated: true } user
            || !int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int userId))
        {
            return UserPreferenceValues.Defaults with { MacAddressFormat = instanceFormat };
        }

        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var stored = await db.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.ItemsPerPage, candidate.MacAddressFormat })
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            return UserPreferenceValues.Defaults with { MacAddressFormat = instanceFormat };
        }

        // Zéro/Default = rien choisi (voir GlpiUser.ItemsPerPage et MacAddressFormat.Default) : les
        // comptes existants n'ont pas eu à être renseignés lors de la migration, et le réglage de
        // l'instance continue de s'appliquer pour eux.
        return new UserPreferenceValues(
            stored.ItemsPerPage > 0 ? stored.ItemsPerPage : UserPreferenceValues.Defaults.ItemsPerPage,
            stored.MacAddressFormat == MacAddressFormat.Default ? instanceFormat : stored.MacAddressFormat);
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
