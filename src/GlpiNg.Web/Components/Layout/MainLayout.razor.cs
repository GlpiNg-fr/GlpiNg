using GlpiNg.Modules.Abstractions.Menu;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Layout;

public partial class MainLayout : IDisposable
{
    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private IEnumerable<IMenuProvider> MenuProviders { get; set; } = null!;

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IProfileRightsProvider Rights { get; set; } = null!;

    /// <summary>
    /// Section de la page affichée, ou <c>null</c> si elle ne relève d'aucun droit. Recalculée à
    /// chaque navigation : c'est ce qui garde la navigation interne au circuit, que le middleware
    /// <c>SectionAccessMiddleware</c> ne voit pas passer.
    /// </summary>
    private ProfileSection? CurrentSection => ProfileSectionMap.ForPath("/" + Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0]);

    private bool CanViewCurrentPage => Rights.Current.CanRead(CurrentSection);

    /// <summary>Section refusée par <c>SectionAccessMiddleware</c>, passée en query string lors du renvoi vers l'accueil.</summary>
    private string? DeniedSection =>
        Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(Nav.Uri).Query).TryGetValue("denied", out var values)
            ? values.FirstOrDefault()
            : null;

    private sealed record NavItem(string Label, string? Href = null, string? Icon = null);
    private sealed record NavGroup(string Key, string Icon, string Label, List<NavItem> Items);

    // Groupes/entrées non encore repris par un module : chaque module qui prend en charge
    // un domaine retire ses entrées d'ici et les fournit via un IMenuProvider (voir
    // GlpiNg.Modules.Inventory.InventoryMenuProvider pour "parc" et les entrées Agents/
    // Déploiements de "outils"). L'ordre d'apparition dans le menu suit GroupOrder,
    // pas cette liste.
    private static readonly List<NavGroup> HostGroups =
    [
        new("assistance", "ti-headset", "Assistance",
        [
            new("Tickets", Icon: "ti-ticket"),
            new("Problèmes", Icon: "ti-alert-triangle"),
            new("Changements", Icon: "ti-replace"),
            new("Planning", Icon: "ti-calendar"),
            new("Statistiques", Icon: "ti-chart-bar"),
        ]),
        new("gestion", "ti-wallet", "Gestion",
        [
            new("Licences", Icon: "ti-license"),
            new("Budgets", Icon: "ti-report-money"),
            new("Fournisseurs", Icon: "ti-truck"),
            new("Contacts", Icon: "ti-address-book"),
            new("Contrats", Icon: "ti-file-text"),
            new("Documents", Icon: "ti-file"),
            new("Lignes téléphoniques", Icon: "ti-phone-call"),
            new("Certificats", Icon: "ti-certificate"),
            new("Data centers", Icon: "ti-building-warehouse"),
            new("Clusters", Icon: "ti-affiliate"),
            new("Domaines", Icon: "ti-world-www"),
            new("Applicatifs", Icon: "ti-apps"),
            new("Bases de données", Icon: "ti-database"),
        ]),
        new("outils", "ti-briefcase", "Outils",
        [
            new("Réservations", Icon: "ti-calendar-event"),
            new("Rapports", Icon: "ti-report"),
            new("Base de connaissances", Icon: "ti-book"),
        ]),
        new("administration", "ti-shield", "Administration",
        [
            new("Utilisateurs", "/admin/users", "ti-user"),
            new("Groupes", "/admin/groups", "ti-users"),
            new("Entités", "/admin/entities", "ti-sitemap"),
            new("Profils", "/admin/profiles", "ti-id-badge"),
            new("File d'attente des notifications", "/config/notifications/queue", "ti-bell"),
            new("Journaux", "/admin/logs", "ti-history"),
            new("Formulaires", Icon: "ti-forms"),
        ]),
        new("configuration", "ti-settings", "Configuration",
        [
            new("Générale", "/config", "ti-settings"),
            new("Actifs personnalisés", Icon: "ti-tool"),
            new("Composants", Icon: "ti-puzzle"),
            new("Notifications", "/config/notifications", "ti-bell-ringing"),
            new("Webhooks", "/config/webhooks", "ti-webhook"),
            new("Niveaux de services", Icon: "ti-clipboard-check"),
            new("Unicité des champs", Icon: "ti-fingerprint"),
            new("Actions automatiques", "/config/automatic-actions", "ti-robot"),
            new("Authentification", "/config/auth", "ti-key"),
            new("Clients OAuth", "/config/oauth-clients", "ti-lock-access"),
            new("Collecteurs", Icon: "ti-mail"),
            new("Liens externes", "/config/external-links", "ti-external-link"),
            new("Plugins", Icon: "ti-plug"),
        ]),
    ];

    // Ordre canonique des groupes dans le menu. Un groupe n'apparaît que s'il a du contenu
    // (host et/ou module) ; les groupes hors de cette liste (nouveau domaine apporté par un
    // futur module) sont ajoutés à la suite, dans l'ordre où les modules les déclarent.
    private static readonly string[] GroupOrder =
        ["parc", "assistance", "gestion", "outils", "administration", "configuration"];

    // Onglets de "/tools/deployments" (voir DeploymentTopMenu) : ce ne sont pas des entrées de
    // menu latéral, donc BuildBreadcrumb ne peut pas les retrouver via Groups. On ajoute leur
    // libellé comme dernier segment du fil d'Ariane à la place du <h3> qu'affichait chaque page.
    private static readonly Dictionary<string, (string Icon, string Label)> DeploymentSubPages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/tools/deployments/computer-groups"] = ("ti-devices", "Groupe d'ordinateurs"),
        ["/tools/deployments/config"] = ("ti-settings", "Configuration générale"),
        ["/tools/deployments/packages"] = ("ti-package", "Gestion de paquets"),
        ["/tools/deployments/supervision"] = ("ti-activity", "Supervision / Logs"),
        ["/tools/deployments/collects"] = ("ti-file-database", "Informations de collecte"),
        ["/tools/deployments/timeslots"] = ("ti-calendar-time", "Créneau horaire"),
        ["/tools/deployments/mirrors"] = ("ti-server-2", "Serveurs miroirs"),
        ["/tools/deployments/interaction-templates"] = ("ti-message-2-question", "Gabarits d'interactions utilisateurs"),
    };

    private List<NavGroup> Groups { get; set; } = [];

    private string? _expandedGroup;
    private bool _collapsed;
    private bool _searchOpen;
    private string _searchTerm = string.Empty;

    private List<(string Icon, string Label, string? Href)> Breadcrumb { get; set; } = [];

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private string _userName = string.Empty;
    private string _userInitial = string.Empty;
    private int? _userId;

    protected override async Task OnInitializedAsync()
    {
        var modules = await SettingsStore.ReadSectionAsync<ModulesSettings>("ModulesSettings");
        Groups = BuildGroups(modules);
        Nav.LocationChanged += OnLocationChanged;
        Breadcrumb = BuildBreadcrumb();
        _expandedGroup = FindGroupForPath();

        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            _userName = authState.User.Identity?.Name ?? string.Empty;
            string? userIdClaim = authState.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            _userId = int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        _userInitial = _userName.Length > 0 ? _userName[..1].ToUpperInvariant() : "?";

        if (_userId is int uid)
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
            _collapsed = await db.Users.AsNoTracking()
                .Where(u => u.Id == uid)
                .Select(u => u.SidebarCollapsed)
                .FirstOrDefaultAsync();
        }
    }

    private List<NavGroup> BuildGroups(ModulesSettings modules)
    {
        var byKey = new Dictionary<string, NavGroup>();
        var ordered = new List<NavGroup>();

        void AddOrMerge(string key, string icon, string label, IEnumerable<NavItem> items)
        {
            var enabledItems = items.Where(i => modules.IsEnabled(key, i.Label));

            if (byKey.TryGetValue(key, out var existing))
            {
                existing.Items.AddRange(enabledItems);
                return;
            }

            var group = new NavGroup(key, icon, label, [.. enabledItems]);
            byKey[key] = group;
            ordered.Add(group);
        }

        var hostByKey = HostGroups.ToDictionary(g => g.Key);
        var moduleGroups = MenuProviders.SelectMany(p => p.GetMenuGroups()).ToList();

        foreach (var key in GroupOrder)
        {
            if (!modules.IsModuleEnabled(key))
            {
                continue;
            }

            // Droits par profil : un groupe dont l'utilisateur n'a pas au moins la lecture n'est
            // pas affiché. Le masquer ne suffit pas à protéger la route — c'est le rôle du
            // middleware et de CanViewCurrentPage — mais évite de proposer des liens en impasse.
            if (!Rights.Current.CanRead(ProfileSectionMap.ForMenuGroupKey(key)))
            {
                continue;
            }

            if (hostByKey.TryGetValue(key, out var hostGroup))
            {
                AddOrMerge(hostGroup.Key, hostGroup.Icon, hostGroup.Label, hostGroup.Items);
            }

            foreach (var moduleGroup in moduleGroups.Where(g => g.Key == key))
            {
                AddOrMerge(moduleGroup.Key, moduleGroup.Icon, moduleGroup.Label, moduleGroup.Items.Select(i => new NavItem(i.Label, i.Href, i.Icon)));
            }
        }

        // Groupes hors du GroupOrder connu : nouveau domaine apporté par un module,
        // pas encore intégré au menu hôte.
        foreach (var moduleGroup in moduleGroups.Where(g => !GroupOrder.Contains(g.Key) && modules.IsModuleEnabled(g.Key)))
        {
            AddOrMerge(moduleGroup.Key, moduleGroup.Icon, moduleGroup.Label, moduleGroup.Items.Select(i => new NavItem(i.Label, i.Href, i.Icon)));
        }

        return ordered;
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        Breadcrumb = BuildBreadcrumb();
        _expandedGroup = FindGroupForPath() ?? _expandedGroup;
        InvokeAsync(StateHasChanged);
    }

    private string? FindGroupForPath()
    {
        var path = "/" + new Uri(Nav.Uri).AbsolutePath.Trim('/');
        return Groups.FirstOrDefault(g => g.Items.Any(i => i.Href is not null &&
            (i.Href == "/" ? path == "/" : path.StartsWith(i.Href, StringComparison.OrdinalIgnoreCase))))?.Key;
    }

    private void ToggleGroup(string key)
    {
        _expandedGroup = _expandedGroup == key ? null : key;
    }

    private async Task ToggleSidebarCollapse()
    {
        _collapsed = !_collapsed;

        if (_userId is int userId)
        {
            await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user is not null)
            {
                user.SidebarCollapsed = _collapsed;
                await db.SaveChangesAsync();
            }
        }
    }

    private void ToggleSearch()
    {
        _searchOpen = !_searchOpen;
        _searchTerm = string.Empty;
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _searchTerm = e.Value?.ToString() ?? string.Empty;
    }

    private bool IsSearching => !string.IsNullOrWhiteSpace(_searchTerm);

    private bool GroupMatches(NavGroup group) => !IsSearching
        || group.Label.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase)
        || group.Items.Any(i => i.Label.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase));

    private List<NavItem> FilteredItems(NavGroup group) => !IsSearching || group.Label.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase)
        ? group.Items
        : group.Items.Where(i => i.Label.Contains(_searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

    private bool IsGroupOpen(NavGroup group) => IsSearching || _expandedGroup == group.Key;

    /// <summary>
    /// Vrai quand ce lien désigne la page affichée.
    ///
    /// C'est la position dans le fil qui décidait jusqu'ici du caractère cliquable : le dernier
    /// élément était toujours du texte mort. Sur une fiche — « Accueil / Parc / Ordinateurs » vu
    /// depuis /parc/computer/12 — « Ordinateurs » est le dernier élément mais désigne la liste,
    /// pas la page courante : c'est exactement le lien dont on a besoin pour remonter.
    /// </summary>
    private bool IsCurrentPage(string href)
    {
        string path = "/" + new Uri(Nav.Uri).AbsolutePath.Trim('/');
        return string.Equals(path, "/" + href.Trim('/'), StringComparison.OrdinalIgnoreCase);
    }

    private List<(string Icon, string Label, string? Href)> BuildBreadcrumb()
    {
        var path = "/" + new Uri(Nav.Uri).AbsolutePath.Trim('/');
        var items = new List<(string Icon, string Label, string? Href)>
        {
            ("ti-home", "Accueil", "/")
        };

        if (path == "/") return items;

        NavItem? bestMatch = null;
        NavGroup? parentGroup = null;
        int bestLen = 0;

        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                if (item.Href is null) continue;
                if (path.StartsWith(item.Href, StringComparison.OrdinalIgnoreCase)
                    && item.Href.Length > bestLen)
                {
                    bestMatch = item;
                    parentGroup = group;
                    bestLen = item.Href.Length;
                }
            }
        }

        if (parentGroup is not null)
        {
            // Un groupe du menu latéral n'a pas de page à lui. Il mène donc à sa première entrée,
            // comme le fait le clic sur un groupe : sans cela « Parc » était un mot mort au milieu
            // du fil d'Ariane, alors que c'est précisément par là qu'on remonte.
            string? groupHref = parentGroup.Items.FirstOrDefault(item => item.Href is not null)?.Href;
            items.Add((parentGroup.Icon, parentGroup.Label, groupHref));
        }

        if (bestMatch is not null)
        {
            items.Add((bestMatch.Icon ?? "ti-point", bestMatch.Label, bestMatch.Href));
        }

        if (DeploymentSubPages.TryGetValue(path, out var subPage))
        {
            items.Add((subPage.Icon, subPage.Label, null));
        }

        return items;
    }

    public void Dispose()
    {
        Nav.LocationChanged -= OnLocationChanged;
    }
}
