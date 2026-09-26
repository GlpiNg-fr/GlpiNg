using System.Security.Claims;
using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Preferences;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.MyAccount;

/// <summary>
/// Préférences personnelles de l'utilisateur connecté (menu utilisateur de la topbar — voir
/// MainLayout.razor), reprises de l'onglet « Personnalisation » de GLPI.
///
/// Chaque réglage peut rester sur « Réglage de l'instance » : il suit alors Configuration &gt;
/// Valeurs par défaut (DefaultValuesSettings), et changer celle-ci touche tous les comptes qui ne
/// l'ont pas surchargé. Ce choix est stocké comme une valeur nulle sur le compte, jamais comme une
/// copie de la valeur de l'instance, qui figerait le compte à l'ancienne valeur.
///
/// Un rechargement complet (forceLoad) suit l'enregistrement : les préférences sont lues une fois
/// par circuit (voir UserPreferencesProvider), et MainLayout ne lit SidebarCollapsed qu'à
/// l'initialisation — un nouveau circuit est le seul moyen que tout reflète le changement.
/// </summary>
public partial class Preferences : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    /// <summary>Valeur des listes qui signifie « suivre l'instance » : stockée comme null.</summary>
    private const string Inherit = "";

    /// <summary>Onglets de la page, dans l'ordre de la barre latérale — même présentation que les fiches.</summary>
    private static readonly (string Key, string Icon, string Label)[] Tabs =
    [
        ("affichage", "ti-layout-sidebar", "Affichage"),
        ("apparence", "ti-palette", "Apparence"),
        ("dates", "ti-calendar-time", "Dates et heures"),
        ("personnes", "ti-users", "Personnes"),
        ("historique", "ti-history", "Historique"),
        ("exports", "ti-file-export", "Exports"),
        ("notifications", "ti-bell", "Notifications"),
    ];

    private string _activeTab = "affichage";

    private GlpiNgDbContext? _db;
    private int? _userId;
    private bool _isSaving;

    private bool _sidebarCollapsed;
    private int _itemsPerPage;
    private MacAddressFormat _macAddressFormat = MacAddressFormat.Default;

    // Réglages hérités de l'instance : Inherit ("") ou la valeur choisie, au format persisté.
    private string _dateFormat = Inherit;
    private string _timezone = Inherit;
    private string _nameOrder = Inherit;
    private string _csvDelimiter = Inherit;
    private string _historyOrder = Inherit;
    private string _showIds = Inherit;
    private string _showCounters = Inherit;
    private string _notifyOnMyChanges = Inherit;
    private string _colorPalette = Inherit;
    private string _highContrast = Inherit;

    private string InstancePaletteLabel =>
        PaletteCatalog.LabelOf(_instance.ColorPalette);

    /// <summary>Valeurs de l'instance, montrées dans chaque option « Réglage de l'instance » : sans
    /// elles, ce choix ne dirait pas ce qu'il applique.</summary>
    private DefaultValuesSettings _instance = new();
    private MacAddressFormat _instanceMacFormat = MacAddressFormatter.Fallback;

    private List<(string Id, string Label)> _timezones = [];

    protected override async Task OnInitializedAsync()
    {
        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            string? userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _userId = int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        if (_userId is not int uid)
        {
            return;
        }

        _instanceMacFormat = MacAddressFormatter.FromSettingsValue(
            (await SettingsStore.ReadSectionAsync<GeneralSettings>("GeneralSettings")).MacAddressFormat);
        _instance = await SettingsStore.ReadSectionAsync<DefaultValuesSettings>("DefaultValuesSettings");

        _db = await DbFactory.CreateDbContextAsync();

        GlpiUser? stored = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == uid);
        if (stored is not null)
        {
            _sidebarCollapsed = stored.SidebarCollapsed;
            _itemsPerPage = stored.ItemsPerPage;
            _macAddressFormat = stored.MacAddressFormat;
            _dateFormat = stored.DateFormat ?? Inherit;
            _timezone = stored.Timezone ?? Inherit;
            _nameOrder = stored.FullNameOrder ?? Inherit;
            _csvDelimiter = stored.CsvDelimiter ?? Inherit;
            _historyOrder = stored.HistoryOrder ?? Inherit;
            _showIds = FromBool(stored.ShowGlpiIds);
            _showCounters = FromBool(stored.ShowCounters);
            _notifyOnMyChanges = FromBool(stored.NotifyOnMyChanges);
            _colorPalette = stored.ColorPalette ?? Inherit;
            _highContrast = FromBool(stored.HighContrast);
        }

        _timezones = BuildTimezones(_timezone, _instance.Timezone);
    }

    /// <summary>
    /// Fuseaux proposés : ceux du système, plus celui de l'instance et celui déjà choisi s'ils n'y
    /// figurent pas sous cet identifiant — la configuration de l'instance utilise des noms IANA
    /// (« Europe/Paris ») là où Windows liste ses propres identifiants.
    /// </summary>
    private static List<(string Id, string Label)> BuildTimezones(params string?[] mustInclude)
    {
        List<(string Id, string Label)> zones = [.. TimeZoneInfo.GetSystemTimeZones().Select(zone => (zone.Id, zone.DisplayName))];

        foreach (string? id in mustInclude)
        {
            if (!string.IsNullOrEmpty(id) && id != "server" && !zones.Exists(zone => zone.Id == id))
            {
                TimeZoneInfo resolved = UserPreferencesProvider.ResolveTimeZone(id);
                zones.Insert(0, (id, $"{id} — {resolved.DisplayName}"));
            }
        }

        return zones;
    }

    private static string FromBool(bool? value) => value switch
    {
        true => "true",
        false => "false",
        null => Inherit,
    };

    private static bool? ToBool(string value) => value switch
    {
        "true" => true,
        "false" => false,
        _ => null,
    };

    private static string? ToNullable(string value) => value == Inherit ? null : value;

    // ---- Libellés des valeurs de l'instance --------------------------------------------------

    private static string DateFormatLabel(string? value) => value switch
    {
        "ymd" => "AAAA-MM-JJ",
        "mdy" => "MM-JJ-AAAA",
        _ => "JJ-MM-AAAA",
    };

    private static string NameOrderLabel(string? value) => value == "PrenomNom" ? "Prénom Nom" : "Nom Prénom";

    private static string HistoryOrderLabel(string? value) =>
        value == "reverse" ? "nouvelles entrées en premier" : "anciennes entrées en premier";

    private static string TimezoneLabel(string? value) =>
        string.IsNullOrEmpty(value) || value == "server" ? "fuseau du serveur" : value;

    private static string YesNo(bool value) => value ? "Oui" : "Non";

    private string InstanceCountersLabel => _instance.ShowCounters switch
    {
        2 => "Jamais",
        1 => "Oui",
        _ => "Non",
    };

    /// <summary>« Jamais » côté instance interdit les compteurs à tous, choix personnel compris.</summary>
    private bool CountersForbidden => _instance.ShowCounters == 2;

    /// <summary>Taille héritée : la valeur de l'instance, ramenée à une taille du sélecteur des listes.</summary>
    private int InstanceItemsPerPage => UserPreferencesProvider.SnapItemsPerPage(_instance.ResultsPerPage);

    /// <summary>
    /// Aperçu de l'heure qu'il est, avec le format et le fuseau sélectionnés — avant d'enregistrer :
    /// c'est ce qui rend lisible un choix comme « MM-JJ-AAAA » ou un fuseau lointain.
    /// </summary>
    private string Preview
    {
        get
        {
            UserPreferenceValues values = new()
            {
                DateFormat = UserPreferencesProvider.ParseDateFormat(ToNullable(_dateFormat) ?? _instance.DateFormat),
                TimeZone = UserPreferencesProvider.ResolveTimeZone(ToNullable(_timezone) ?? _instance.Timezone),
            };

            return $"{values.DateTime(DateTime.UtcNow)} ({values.TimeZone.DisplayName})";
        }
    }

    private async Task SaveAsync()
    {
        if (_db is null || _userId is not int uid)
        {
            return;
        }

        _isSaving = true;

        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == uid);
            if (user is null)
            {
                return;
            }

            user.SidebarCollapsed = _sidebarCollapsed;
            user.ItemsPerPage = _itemsPerPage;
            user.MacAddressFormat = _macAddressFormat;
            user.DateFormat = ToNullable(_dateFormat);
            user.Timezone = ToNullable(_timezone);
            user.FullNameOrder = ToNullable(_nameOrder);
            user.CsvDelimiter = ToNullable(_csvDelimiter);
            user.HistoryOrder = ToNullable(_historyOrder);
            user.ShowGlpiIds = ToBool(_showIds);
            user.ShowCounters = ToBool(_showCounters);
            user.NotifyOnMyChanges = ToBool(_notifyOnMyChanges);
            user.ColorPalette = ToNullable(_colorPalette);
            user.HighContrast = ToBool(_highContrast);

            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Préférences enregistrées."));
            Nav.NavigateTo(Nav.Uri, forceLoad: true);
        }
        finally
        {
            _isSaving = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
