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
/// Enregistré en Scoped, et le résultat est mémorisé : une requête ou un circuit Blazor lit une
/// fois, pas une par tableau affiché. Hors session applicative — protocole agent, cron, jeton
/// OAuth — on rend les valeurs de l'instance plutôt que d'échouer : une préférence d'affichage n'a
/// aucun sens là, et rien ne doit dépendre de sa présence.
///
/// La lecture vise la base et non les revendications de la session, contrairement à
/// <see cref="ProfileRightsProvider"/> : un droit ne change qu'à la connexion suivante, ce qui est
/// acceptable pour une habilitation, alors qu'un réglage que l'utilisateur vient d'enregistrer doit
/// s'appliquer tout de suite.
///
/// <see cref="Current"/> est préchargé par <see cref="UserPreferencesPreloader"/> au début de
/// chaque requête et de chaque circuit, pour que le balisage Razor l'ait sans attendre.
/// </summary>
public sealed class UserPreferencesProvider(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    SettingsCacheService settings,
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services,
    ILogger<UserPreferencesProvider> logger) : IUserPreferences
{
    /// <summary>Tailles proposées au pied de chaque liste : la valeur par défaut doit en être une.</summary>
    public static readonly int[] ItemsPerPageOptions = [25, 50, 100, 200, 500];

    private UserPreferenceValues? _cached;
    private bool _warnedNotLoaded;

    public UserPreferenceValues Current
    {
        get
        {
            if (_cached is { } cached)
            {
                return cached;
            }

            // Ne devrait pas arriver : le préchargement couvre requêtes et circuits. Si c'est le
            // cas, on affiche avec les valeurs par défaut plutôt que de bloquer le rendu.
            if (!_warnedNotLoaded)
            {
                _warnedNotLoaded = true;
                logger.LogDebug("Préférences lues avant leur préchargement : valeurs par défaut utilisées.");
            }

            return UserPreferenceValues.Defaults;
        }
    }

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
        GeneralSettings general = await settings.ReadSectionAsync<GeneralSettings>("GeneralSettings", cancellationToken);
        DefaultValuesSettings instance = await settings.ReadSectionAsync<DefaultValuesSettings>("DefaultValuesSettings", cancellationToken);

        MacAddressFormat instanceMac = MacAddressFormatter.FromSettingsValue(general.MacAddressFormat);

        GlpiUser? stored = null;
        int? userId = null;

        if (GetUser() is { Identity.IsAuthenticated: true } user
            && int.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out int id))
        {
            userId = id;

            await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
            stored = await db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        }

        // « Jamais » (2) côté instance interdit les compteurs à tous : c'est ce qui le distingue de
        // « Non » (0), que chacun peut contredire. Même sens que dans GLPI.
        bool countersForbidden = instance.ShowCounters == 2;

        return new UserPreferenceValues
        {
            UserId = userId,

            // Zéro/Default = rien choisi (voir GlpiUser.ItemsPerPage et MacAddressFormat.Default).
            ItemsPerPage = stored is { ItemsPerPage: > 0 } ? stored.ItemsPerPage : SnapItemsPerPage(instance.ResultsPerPage),
            MacAddressFormat = stored is null || stored.MacAddressFormat == MacAddressFormat.Default ? instanceMac : stored.MacAddressFormat,

            DateFormat = ParseDateFormat(stored?.DateFormat ?? instance.DateFormat),
            TimeZone = ResolveTimeZone(stored?.Timezone ?? instance.Timezone),
            NameOrder = (stored?.FullNameOrder ?? instance.FullNameOrder) == "PrenomNom" ? NameDisplayOrder.FirstLast : NameDisplayOrder.LastFirst,
            CsvDelimiter = NormalizeDelimiter(stored?.CsvDelimiter ?? instance.CsvDelimiter),
            ShowIds = stored?.ShowGlpiIds ?? instance.ShowGlpiIds,
            NewestFirst = (stored?.HistoryOrder ?? instance.HistoryOrder) == "reverse",
            ShowTabCounters = !countersForbidden && (stored?.ShowCounters ?? instance.ShowCounters == 1),
            NotifyOnMyChanges = stored?.NotifyOnMyChanges ?? instance.NotifyOnMyChanges,
            Palette = PaletteCatalog.KeyOf(stored?.ColorPalette ?? instance.ColorPalette),
            HighContrast = stored?.HighContrast ?? instance.HighContrast,
            Language = LanguageCatalog.Normalize(stored?.Language ?? instance.Language),
        };
    }

    /// <summary>
    /// La valeur de l'instance est libre (champ numérique), le sélecteur des listes ne l'est pas :
    /// 20 deviendrait une entrée vide. On retient la plus petite taille proposée qui la contient.
    /// </summary>
    public static int SnapItemsPerPage(int value) =>
        ItemsPerPageOptions.FirstOrDefault(option => option >= value, ItemsPerPageOptions[^1]);

    public static DateDisplayFormat ParseDateFormat(string? value) => value switch
    {
        "ymd" => DateDisplayFormat.YearMonthDay,
        "mdy" => DateDisplayFormat.MonthDayYear,
        _ => DateDisplayFormat.DayMonthYear,
    };

    public static string ToSettingsValue(DateDisplayFormat format) => format switch
    {
        DateDisplayFormat.YearMonthDay => "ymd",
        DateDisplayFormat.MonthDayYear => "mdy",
        _ => "dmy",
    };

    /// <summary>
    /// « server » ou un identifiant inconnu : le fuseau du serveur. Les identifiants IANA
    /// (« Europe/Paris ») comme Windows sont acceptés — .NET convertit l'un vers l'autre.
    /// </summary>
    public static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == "server")
        {
            return TimeZoneInfo.Local;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private static string NormalizeDelimiter(string? value) => value is "," or ";" or "\t" ? value : ";";

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
