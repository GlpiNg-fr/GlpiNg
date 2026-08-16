namespace GlpiNg.Web.Models;

/// <summary>Action possible quand un utilisateur restauré/supprimé est constaté dans l'annuaire LDAP.</summary>
public enum LdapUserPresenceAction
{
    DoNothing = 0,
    Restore = 1,
    MoveToTrash = 2,
    Delete = 3,
    Deactivate = 4,
}

/// <summary>
/// Modèle de l'onglet "Configuration" de la page /config/auth (auth.settings.php côté
/// GLPI). Stocké via SettingsCacheService (section "AuthSettings"), au même titre que les
/// autres onglets de /config. Seul <see cref="AutoAddUsersFromExternalAuth"/> est
/// aujourd'hui branché sur une vérification réelle (voir AccountController.Login) : les
/// autres champs sont conservés pour la fidélité avec le formulaire GLPI mais ne pilotent
/// aucune synchronisation automatique — GlpiNg n'a pas de tâche planifiée de sync LDAP.
/// </summary>
public class AuthSettings
{
    public bool AutoAddUsersFromExternalAuth { get; set; }

    public bool AddUserWithoutRightFromLdap { get; set; }

    public LdapUserPresenceAction RestoredUserAction { get; set; } = LdapUserPresenceAction.DoNothing;

    public LdapUserPresenceAction DeletedUserAction { get; set; } = LdapUserPresenceAction.MoveToTrash;

    public LdapUserPresenceAction DeletedUserGroupsAction { get; set; } = LdapUserPresenceAction.DoNothing;

    public LdapUserPresenceAction DeletedUserHabilitationsAction { get; set; } = LdapUserPresenceAction.DoNothing;
}
