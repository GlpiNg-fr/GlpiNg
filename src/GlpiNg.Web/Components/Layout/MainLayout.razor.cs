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
        // « assistance » n'a plus d'entrée ici : tout le groupe vient du module Assistance
        // (AssistanceMenuProvider), qui le crée lui-même.
        new("gestion", "ti-wallet", "Gestion",
        [
            // Tout le reste du groupe est apporté par le module Gestion (voir
            // ManagementMenuProvider) : le déclarer ici aussi l'afficherait en double. Seuls les
            // documents restent à l'hôte, qui les porte pour tous les types d'objets.
            new("Documents", "/management/documents", "ti-file"),
        ]),
        new("outils", "ti-briefcase", "Outils",
        [
            new("Réservations", Icon: "ti-calendar-event"),
            new("Notes", "/tools/notes", "ti-notes"),
            new("Rapports", "/tools/reports", "ti-report"),
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
            new("Actifs personnalisés", "/config/custom-assets", "ti-tool"),
            new("Notifications", "/config/notifications", "ti-bell-ringing"),
            new("Webhooks", "/config/webhooks", "ti-webhook"),
            new("Unicité des champs", "/config/field-unicity", "ti-fingerprint"),
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
        Groups = BuildGroups(modules, await LoadCustomAssetItemsAsync());
        Nav.LocationChanged += OnLocationChanged;
        SettingsStore.SectionSaved += OnSettingsSectionSaved;
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

    /// <summary>
    /// Entrées du menu Parc pour les types d'actifs personnalisés actifs (voir
    /// <c>Models/CustomAssets</c>). Lues en base à chaque ouverture de circuit plutôt que déclarées
    /// en dur ou par un <c>IMenuProvider</c> : ces types naissent d'un formulaire de configuration,
    /// donc la liste change sans redémarrage ni recompilation.
    /// </summary>
    private async Task<List<NavItem>> LoadCustomAssetItemsAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        return await db.Set<Models.CustomAssets.CustomAssetDefinition>()
            .AsNoTracking()
            .Where(definition => definition.IsActive)
            .OrderBy(definition => definition.LabelPlural)
            .Select(definition => new NavItem(
                definition.LabelPlural,
                "/parc/custom/" + definition.SystemName,
                definition.Icon))
            .ToListAsync();
    }

    private List<NavGroup> BuildGroups(ModulesSettings modules, List<NavItem> customAssetItems)
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

            // Les types d'actifs personnalisés ferment le groupe Parc, après les types livrés par
            // le module Inventory : ils s'ajoutent au parc plutôt que de s'y intercaler.
            if (key == "parc" && customAssetItems.Count > 0)
            {
                AddOrMerge("parc", "ti-server", "Parc", customAssetItems);
            }
        }

        // Groupes hors du GroupOrder connu : nouveau domaine apporté par un module,
        // pas encore intégré au menu hôte.
        foreach (var moduleGroup in moduleGroups.Where(g => !GroupOrder.Contains(g.Key) && modules.IsModuleEnabled(g.Key)))
        {
            AddOrMerge(moduleGroup.Key, moduleGroup.Icon, moduleGroup.Label, moduleGroup.Items.Select(i => new NavItem(i.Label, i.Href, i.Icon)));
        }

        // Les entrées sans adresse — ce qui reste à écrire — descendent en fin de groupe. Les
        // entrées de l'hôte étant fusionnées avant celles des modules, une fonctionnalité reprise
        // par un module se retrouvait sinon listée après les impasses qu'elle était venue
        // remplacer : « Changements » avant « Tickets », « Plugins » avant « Niveaux de services ».
        // Tri stable, donc l'ordre voulu est préservé de part et d'autre.
        foreach (var group in ordered)
        {
            List<NavItem> sorted = [.. group.Items.OrderBy(item => item.Href is null)];

            group.Items.Clear();
            group.Items.AddRange(sorted);
        }

        return ordered;
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        Breadcrumb = BuildBreadcrumb();
        _expandedGroup = FindGroupForPath() ?? _expandedGroup;
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Un module ou une entrée de menu vient d'être activé ou désactivé dans /config — peut-être par
    /// un autre utilisateur : le menu de ce circuit se reconstruit sur place, sans rechargement. Levé
    /// sur le fil de l'enregistrement, d'où le passage par InvokeAsync vers le circuit de ce menu.
    /// </summary>
    private void OnSettingsSectionSaved(string sectionName)
    {
        if (sectionName != "ModulesSettings")
        {
            return;
        }

        _ = InvokeAsync(async () =>
        {
            try
            {
                var modules = await SettingsStore.ReadSectionAsync<ModulesSettings>("ModulesSettings");
                Groups = BuildGroups(modules, await LoadCustomAssetItemsAsync());
                Breadcrumb = BuildBreadcrumb();
                _expandedGroup = FindGroupForPath() ?? (Groups.Any(g => g.Key == _expandedGroup) ? _expandedGroup : null);
                StateHasChanged();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                // Circuit fermé entre l'enregistrement et la mise à jour : plus de menu à redessiner.
            }
        });
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
        SettingsStore.SectionSaved -= OnSettingsSectionSaved;
    }
}
