using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Rattache une route et un type d'entité à la section de menu qui en porte le droit. C'est la
/// table de correspondance unique dont se servent le filtrage du menu, le garde-fou de routes
/// (<c>SectionAccessMiddleware</c> et <c>MainLayout</c>) et le refus d'écriture du
/// <c>GlpiNgDbContext</c>.
/// </summary>
public static class ProfileSectionMap
{
    /// <summary>
    /// Préfixes de route, du plus spécifique au plus général. Une route qui ne correspond à aucun
    /// préfixe n'appartient à aucune section et reste donc accessible : c'est le cas du tableau de
    /// bord, de la fiche de compte et des préférences, qui ne relèvent d'aucun droit.
    /// </summary>
    private static readonly (string Prefix, ProfileSection Section)[] RoutePrefixes =
    [
        ("/parc", ProfileSection.Parc),
        ("/tools", ProfileSection.Outils),
        ("/admin", ProfileSection.Administration),
        ("/config", ProfileSection.Configuration),
    ];

    /// <summary>
    /// Section d'une route, ou <c>null</c> si elle n'est soumise à aucun droit.
    /// </summary>
    public static ProfileSection? ForPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        foreach ((string prefix, ProfileSection section) in RoutePrefixes)
        {
            if (path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                return section;
            }
        }

        return null;
    }

    /// <summary>Clé de groupe du menu latéral (voir <c>MainLayout.GroupOrder</c>) vers sa section.</summary>
    public static ProfileSection? ForMenuGroupKey(string key) => key.ToLowerInvariant() switch
    {
        "parc" => ProfileSection.Parc,
        "assistance" => ProfileSection.Assistance,
        "gestion" => ProfileSection.Gestion,
        "outils" => ProfileSection.Outils,
        "administration" => ProfileSection.Administration,
        "configuration" => ProfileSection.Configuration,
        _ => null,
    };

    /// <summary>
    /// Types écrits par n'importe quel utilisateur pour son propre compte, quels que soient ses
    /// droits : préférences d'affichage et bascule du menu latéral. Les exempter est indispensable
    /// — sans ça, un utilisateur qui n'a que le droit « Parc » ne pourrait plus réorganiser son
    /// tableau de bord, puisque ces types relèvent sinon de l'Administration.
    ///
    /// <see cref="GlpiUser"/> en fait partie pour la même raison (<c>SidebarCollapsed</c> est
    /// écrit à chaque repli du menu). L'administration des comptes reste protégée par le garde-fou
    /// de routes sur <c>/admin/users</c>, qui est la protection de premier rang.
    /// </summary>
    private static readonly HashSet<Type> WriteExempt =
    [
        typeof(GlpiUser),
        typeof(DashboardCardPreference),
        typeof(Modules.Inventory.Models.TableColumnPreference),

        // Écrits par le système au nom de l'utilisateur, pas par une action d'administration ou de
        // configuration : les soumettre au droit de la section les rattacherait à un écran que
        // l'utilisateur n'a aucune raison de pouvoir atteindre. Concrètement, sans ces deux
        // exemptions, un utilisateur sans droit d'écriture en Administration échouerait à se
        // déconnecter (le journal trace la déconnexion), et une action de parc qui déclenche une
        // notification échouerait faute de droit sur la Configuration.
        typeof(EventLogEntry),
        typeof(Models.Notifications.QueuedNotification),
    ];

    /// <summary>
    /// Section dont relève l'écriture d'un type d'entité, ou <c>null</c> si l'écriture n'est
    /// soumise à aucun droit.
    ///
    /// Déduit du namespace plutôt qu'énuméré type par type : les entités filles (composants,
    /// historiques, cibles de tâches...) relèvent de la même section que leur parent et sont donc
    /// couvertes sans avoir à les lister, et un type ajouté plus tard l'est aussi.
    /// </summary>
    public static ProfileSection? ForEntityType(Type type)
    {
        if (WriteExempt.Contains(type))
        {
            return null;
        }

        string ns = type.Namespace ?? string.Empty;

        if (ns.StartsWith("GlpiNg.Modules.Deployment", StringComparison.Ordinal))
        {
            // L'actif non géré est un objet de parc, même si c'est la découverte réseau qui
            // l'alimente : il est listé dans le Parc (/parc/unmanaged) et sa conversion crée un
            // actif de parc. Le rattacher à Outils empêcherait un gestionnaire de parc de le
            // traiter — au prix inverse qu'un utilisateur qui n'a qu'Outils ne peut plus le
            // convertir depuis la page de supervision de la découverte.
            return type.Name == nameof(Modules.Deployment.Models.DiscoveredNetworkDevice)
                ? ProfileSection.Parc
                : ProfileSection.Outils;
        }

        // Base de connaissances : articles, catégories, révisions et cibles relèvent tous des
        // Outils, section sous laquelle la page vit (/tools/knowledgebase).
        if (ns.StartsWith("GlpiNg.Modules.KnowledgeBase", StringComparison.Ordinal))
        {
            return ProfileSection.Outils;
        }

        if (ns.StartsWith("GlpiNg.Modules.Inventory", StringComparison.Ordinal))
        {
            // Le module Inventory porte, en plus du parc, des objets qui apparaissent ailleurs
            // dans le menu : les intitulés sont en Configuration, les règles et dictionnaires en
            // Administration, les recherches sauvegardées dans Outils.
            return type.Name switch
            {
                nameof(Modules.Inventory.Models.DropdownItem) => ProfileSection.Configuration,
                nameof(Modules.Inventory.Models.SavedSearch) or nameof(Modules.Inventory.Models.SavedSearchOrder) => ProfileSection.Outils,
                nameof(Modules.Inventory.Models.ComputerRule) or nameof(Modules.Inventory.Models.ComputerRuleCriterion) or nameof(Modules.Inventory.Models.ComputerRuleAction)
                    or nameof(Modules.Inventory.Models.DictionaryRule) or nameof(Modules.Inventory.Models.DictionaryRuleCriterion)
                    or nameof(Modules.Inventory.Models.ImportAssignmentRule) or nameof(Modules.Inventory.Models.ImportAssignmentRuleCriterion) or nameof(Modules.Inventory.Models.ImportAssignmentRuleAction)
                    or nameof(Modules.Inventory.Models.ImportBlacklistEntry) or nameof(Modules.Inventory.Models.RefusedImportLog) => ProfileSection.Administration,
                _ => ProfileSection.Parc,
            };
        }

        if (ns.StartsWith("GlpiNg.Modules.Cron", StringComparison.Ordinal)
            || ns.StartsWith("GlpiNg.Web.Models.Notifications", StringComparison.Ordinal))
        {
            return ProfileSection.Configuration;
        }

        if (ns.StartsWith("GlpiNg.Web.Models", StringComparison.Ordinal))
        {
            return type.Name switch
            {
                nameof(AuthLdapServer) or nameof(AuthMailServer) or nameof(OAuthClient) or nameof(AppSetting)
                    => ProfileSection.Configuration,
                _ => ProfileSection.Administration,
            };
        }

        return null;
    }
}
