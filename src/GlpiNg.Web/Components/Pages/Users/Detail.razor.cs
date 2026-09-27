using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Components.Pages.Users;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    private sealed record FicheTab(string Key, string Icon, string Label, int? Count);

    // Snapshot des champs journalisés dans l'onglet "Historique" (voir DiffFields). Même
    // principe que les fiches Entité/Groupe : peu importe quel bouton "Sauvegarder" a été
    // cliqué (onglet "Utilisateur", "Habilitations", "Préférences" ou "Sécurité"), ce diff
    // générique retrouve les champs réellement modifiés sur le _user partagé en mémoire. Le mot
    // de passe n'est volontairement pas dans ce snapshot (voir SaveAsync : loggé séparément, sans
    // jamais exposer de valeur).
    private sealed record UserSnapshot(
        string UserName, string? FirstName, string? LastName, string? Email, string? Phone,
        string? Location, bool IsActive, bool IsAdmin, bool SidebarCollapsed,
        bool TwoFactorAuthDisabled);

    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    [Parameter]
    public int UserId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private GlpiNgDbContext? _db;
    private GlpiUser? _user;
    private List<GlpiUser> _allUsers = [];
    private List<GlpiGroup> _allGroups = [];
    private List<GlpiEntity> _allEntities = [];
    private Dictionary<int, GlpiEntity> _entitiesById = [];
    private List<GlpiProfile> _allProfiles = [];
    private List<GlpiUserProfile> _userHabilitations = [];
    private List<GlpiUserHistoryEntry> _historyEntries = [];
    private List<FicheTab> _tabs = [];
    private string _activeTabKey = "utilisateur";
    private bool _isSaving;
    private bool _showPasswordFields;
    private string _newPassword = string.Empty;
    private string _newPasswordConfirm = string.Empty;
    private int _groupIdToAdd;
    private int _habEntityIdToAdd;
    private int _habProfileIdToAdd;
    private bool _habIsRecursiveToAdd;
    private string _currentUserName = "Système";
    private UserSnapshot _beforeEdit = null!;
    private int _position;
    private int _total;
    private int? _previousId;
    private int? _nextId;

    protected override async Task OnInitializedAsync()
    {
        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            string? name = authState.User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                _currentUserName = name;
            }
        }

        _db = await DbFactory.CreateDbContextAsync();

        _user = await _db.Users
            .Include(u => u.GroupMemberships).ThenInclude(m => m.Group)
            .Include(u => u.LdapServer)
            .FirstOrDefaultAsync(u => u.Id == UserId);

        if (_user is null)
        {
            return;
        }

        await ReloadReferenceDataAsync();
        _beforeEdit = Snapshot(_user);
        RebuildTabs();

        _total = _allUsers.Count;
        _position = _allUsers.Count(u => u.Id <= UserId);
        _previousId = _allUsers.Where(u => u.Id < UserId).OrderByDescending(u => u.Id).Select(u => (int?)u.Id).FirstOrDefault();
        _nextId = _allUsers.Where(u => u.Id > UserId).OrderBy(u => u.Id).Select(u => (int?)u.Id).FirstOrDefault();
    }

    private async Task ReloadReferenceDataAsync()
    {
        if (_db is null || _user is null)
        {
            return;
        }

        _allUsers = await _db.Users.AsNoTracking().ToListAsync();
        _allGroups = await _db.Groups.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
        _allEntities = await _db.Entities.AsNoTracking().ToListAsync();
        _entitiesById = _allEntities.ToDictionary(e => e.Id);
        _allProfiles = await _db.Profiles.AsNoTracking().OrderBy(p => p.Name).ToListAsync();

        _userHabilitations = await _db.UserProfiles.AsNoTracking()
            .Include(h => h.Entity)
            .Include(h => h.Profile)
            .Where(h => h.UserId == _user.Id)
            .ToListAsync();

        _historyEntries = await _db.UserHistoryEntries.AsNoTracking()
            .Where(h => h.UserId == _user.Id)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
    }

    private void RebuildTabs()
    {
        if (_user is null)
        {
            return;
        }

        // Reprend la liste et l'ordre exacts des onglets de la fiche "Utilisateur" de GLPI
        // (vérifiés sur une instance GLPI réelle via front/user.form.php ; contrairement à
        // Entité/Groupe, GLPI n'a pas d'onglet "Notes" sur la fiche Utilisateur). "Éléments
        // utilisés"/"Éléments gérés" nécessiteraient de relier un objet du parc (module
        // Inventory) à un utilisateur (module Web) — même blocage architectural que "Éléments
        // gérés" sur la fiche Groupe. "Consommables", "Tickets/Problèmes/Changements créés",
        // "Documents", "Réservations", "Certificats", "Licences" et "Contrats" dépendent chacun
        // d'un module entier absent de GlpiNg. "Liens" suppose les intitulés "Liens" de GLPI
        // (absents). "Synchronisation" a un contenu réel depuis l'ajout de l'annuaire LDAP (voir
        // AuthLdapServer/LdapAuthenticationService). Les autres onglets ont un contenu réel, voir
        // le @switch de Detail.razor.
        _tabs =
        [
            new("utilisateur", "ti-user", "Utilisateur", null),
            new("habilitations", "ti-key", "Habilitations", _userHabilitations.Count),
            new("groupes", "ti-users", "Groupes", _user.GroupMemberships.Count),
            new("preferences", "ti-adjustments", "Préférences", null),
            new("elementsutilises", "ti-device-desktop", "Éléments utilisés", null),
            new("elementsgeres", "ti-box", "Éléments gérés", null),
            new("securite", "ti-shield", "Sécurité", null),
            new("consommables", "ti-package", "Consommables", null),
            new("tickets", "ti-ticket", "Tickets créés", null),
            new("problemes", "ti-alert-triangle", "Problèmes créés", null),
            new("changements", "ti-replace", "Changements créés", null),
            new("documents", "ti-file", "Documents", null),
            new("reservations", "ti-calendar-event", "Réservations", null),
            new("synchronisation", "ti-refresh", "Synchronisation", null),
            new("liens", "ti-link", "Liens", null),
            new("certificats", "ti-certificate", "Certificats", null),
            new("licences", "ti-license", "Licences", null),
            new("contrats", "ti-file-text", "Contrats", null),
            new("history", "ti-history", "Historique", _historyEntries.Count),
            new("all", "ti-list", "Tous", null),
        ];
    }

    // Messages explicites pour les onglets qui dépendent d'un module absent de GlpiNg, affichés
    // à la place du texte générique "n'est pas encore disponible" (voir Detail.razor).
    private static readonly Dictionary<string, string> UnavailableReasons = new()
    {
        ["elementsutilises"] = "Nécessite de relier les objets du parc (postes, etc.) à un utilisateur — pas encore implémenté (le module Inventory ne dépend pas du module Web où vit GlpiUser).",
        ["elementsgeres"] = "Nécessite de relier les objets du parc (postes, etc.) à un utilisateur propriétaire — pas encore implémenté (le module Inventory ne dépend pas du module Web où vit GlpiUser).",
        ["consommables"] = "Nécessite un module Consommables qui n'existe pas encore dans GlpiNg.",
        ["tickets"] = "Nécessite un module Assistance (tickets, SLA) qui n'existe pas encore dans GlpiNg.",
        ["problemes"] = "Nécessite un module Assistance (problèmes) qui n'existe pas encore dans GlpiNg.",
        ["changements"] = "Nécessite un module Assistance (changements) qui n'existe pas encore dans GlpiNg.",
        ["documents"] = "Nécessite un module de gestion documentaire qui n'existe pas encore dans GlpiNg.",
        ["reservations"] = "Nécessite un module Réservations qui n'existe pas encore dans GlpiNg.",
        ["liens"] = "Nécessite les intitulés « Liens » de GLPI, qui n'existent pas encore dans GlpiNg.",
        ["certificats"] = "Nécessite un module Certificats qui n'existe pas encore dans GlpiNg.",
        ["licences"] = "Nécessite un module Licences (logiciels) qui n'existe pas encore dans GlpiNg.",
        ["contrats"] = "Nécessite un module Contrats qui n'existe pas encore dans GlpiNg.",
    };

    private void SetTab(string key)
    {
        _activeTabKey = key;
    }

    private static UserSnapshot Snapshot(GlpiUser u) => new(
        u.UserName, u.FirstName, u.LastName, u.Email, u.Phone,
        u.Location, u.IsActive, u.IsAdmin, u.SidebarCollapsed,
        u.TwoFactorAuthDisabled);

    private static string YesNo(bool value) => value ? Tr.T("Oui") : Tr.T("Non");

    private string EntityFullName(GlpiEntity entity)
    {
        var parts = new List<string> { entity.Name };
        GlpiEntity current = entity;

        while (current.ParentId is { } parentId && _entitiesById.TryGetValue(parentId, out var parent))
        {
            parts.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" > ", parts);
    }

    private IEnumerable<(string Field, string? Old, string? New)> DiffFields(UserSnapshot before, UserSnapshot after)
    {
        if (before.UserName != after.UserName) yield return ("Identifiant", before.UserName, after.UserName);
        if (before.FirstName != after.FirstName) yield return ("Prénom", before.FirstName, after.FirstName);
        if (before.LastName != after.LastName) yield return ("Nom de famille", before.LastName, after.LastName);
        if (before.Email != after.Email) yield return ("E-mail", before.Email, after.Email);
        if (before.Phone != after.Phone) yield return ("Téléphone", before.Phone, after.Phone);
        if (before.Location != after.Location) yield return ("Lieu", before.Location, after.Location);
        if (before.IsActive != after.IsActive) yield return ("Actif", YesNo(before.IsActive), YesNo(after.IsActive));
        if (before.IsAdmin != after.IsAdmin) yield return ("Administrateur", YesNo(before.IsAdmin), YesNo(after.IsAdmin));
        if (before.SidebarCollapsed != after.SidebarCollapsed) yield return ("Menu réduit", YesNo(before.SidebarCollapsed), YesNo(after.SidebarCollapsed));
        if (before.TwoFactorAuthDisabled != after.TwoFactorAuthDisabled) yield return ("Désactiver l'obligation du 2FA", YesNo(before.TwoFactorAuthDisabled), YesNo(after.TwoFactorAuthDisabled));
    }

    private static string FormatChange(string? oldValue, string? newValue) =>
        $"{(string.IsNullOrEmpty(oldValue) ? "vide" : oldValue)} → {(string.IsNullOrEmpty(newValue) ? "vide" : newValue)}";

    private async Task SaveAsync()
    {
        if (_db is null || _user is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            bool passwordChanged = false;
            if (_showPasswordFields && (!string.IsNullOrEmpty(_newPassword) || !string.IsNullOrEmpty(_newPasswordConfirm)))
            {
                if (_newPassword != _newPasswordConfirm)
                {
                    ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Les mots de passe ne correspondent pas.")));
                    return;
                }

                _user.PasswordHash = Hasher.HashPassword(_user, _newPassword);
                _newPassword = string.Empty;
                _newPasswordConfirm = string.Empty;
                _showPasswordFields = false;
                passwordChanged = true;
            }

            _user.DisplayName = ComputeDisplayName(_user);

            UserSnapshot after = Snapshot(_user);
            List<GlpiUserHistoryEntry> entries = DiffFields(_beforeEdit, after)
                .Select(diff => new GlpiUserHistoryEntry
                {
                    UserId = _user.Id,
                    User = _currentUserName,
                    Field = diff.Field,
                    Description = FormatChange(diff.Old, diff.New)
                })
                .ToList();

            // Le mot de passe n'a pas sa place dans DiffFields (jamais de valeur en clair ou de
            // hash journalisée) : une entrée dédiée, sans valeur, si un changement a eu lieu.
            if (passwordChanged)
            {
                entries.Add(new GlpiUserHistoryEntry
                {
                    UserId = _user.Id,
                    User = _currentUserName,
                    Field = "Mot de passe",
                    Description = "Mot de passe modifié"
                });
            }

            if (entries.Count > 0)
            {
                _db.UserHistoryEntries.AddRange(entries);
            }

            await _db.SaveChangesAsync();
            _beforeEdit = after;
            await ReloadReferenceDataAsync();
            RebuildTabs();
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Utilisateur enregistré.")));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void TogglePasswordFields()
    {
        _showPasswordFields = !_showPasswordFields;
        _newPassword = string.Empty;
        _newPasswordConfirm = string.Empty;
    }

    /// <summary>GLPI calcule le nom complet à partir de Nom/Prénom plutôt que de le laisser saisir séparément.</summary>
    private static string ComputeDisplayName(GlpiUser user)
    {
        string fullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return fullName.Length > 0 ? fullName : user.UserName;
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _user is null)
        {
            return;
        }

        _db.Users.Remove(_user);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/admin/users");
    }

    private IEnumerable<GlpiGroup> AvailableGroups()
    {
        if (_user is null)
        {
            return _allGroups;
        }

        HashSet<int> memberOf = _user.GroupMemberships.Select(m => m.GroupId).ToHashSet();
        return _allGroups.Where(g => !memberOf.Contains(g.Id));
    }

    private async Task AddGroupAsync()
    {
        if (_db is null || _user is null || _groupIdToAdd == 0)
        {
            return;
        }

        _db.GroupUsers.Add(new GlpiGroupUser { GroupId = _groupIdToAdd, UserId = _user.Id });
        await _db.SaveChangesAsync();
        await ReloadMembershipsAsync();

        _groupIdToAdd = 0;
        RebuildTabs();
    }

    private async Task RemoveGroupAsync(int groupId)
    {
        if (_db is null || _user is null)
        {
            return;
        }

        GlpiGroupUser? membership = _user.GroupMemberships.FirstOrDefault(m => m.GroupId == groupId);
        if (membership is null)
        {
            return;
        }

        _db.GroupUsers.Remove(membership);
        await _db.SaveChangesAsync();
        await ReloadMembershipsAsync();

        RebuildTabs();
    }

    // Recharge GroupMemberships depuis la base (avec Include(Group)) plutôt que de patcher la
    // collection en mémoire : même bug que celui corrigé sur la fiche Groupe (voir
    // GlpiNg.Web.Components.Pages.Groups.Detail.ReloadMembersAsync) — EF fait du fixup
    // automatique de la collection dès l'Add/Remove, avant que .Group soit renseigné.
    private async Task ReloadMembershipsAsync()
    {
        if (_db is null || _user is null)
        {
            return;
        }

        await _db.Entry(_user).Collection(u => u.GroupMemberships).Query().Include(m => m.Group).LoadAsync();
    }

    private async Task AddHabilitationAsync()
    {
        if (_db is null || _user is null || _habEntityIdToAdd == 0 || _habProfileIdToAdd == 0)
        {
            return;
        }

        _db.UserProfiles.Add(new GlpiUserProfile
        {
            UserId = _user.Id,
            EntityId = _habEntityIdToAdd,
            ProfileId = _habProfileIdToAdd,
            IsRecursive = _habIsRecursiveToAdd
        });
        await _db.SaveChangesAsync();

        _habEntityIdToAdd = 0;
        _habProfileIdToAdd = 0;
        _habIsRecursiveToAdd = false;
        await ReloadReferenceDataAsync();
        RebuildTabs();
    }

    private async Task RemoveHabilitationAsync(int habilitationId)
    {
        if (_db is null)
        {
            return;
        }

        GlpiUserProfile? habilitation = await _db.UserProfiles.FirstOrDefaultAsync(h => h.Id == habilitationId);
        if (habilitation is null)
        {
            return;
        }

        _db.UserProfiles.Remove(habilitation);
        await _db.SaveChangesAsync();

        await ReloadReferenceDataAsync();
        RebuildTabs();
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
