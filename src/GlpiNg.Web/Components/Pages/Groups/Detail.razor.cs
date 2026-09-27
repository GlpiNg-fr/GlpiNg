using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Components.Pages.Groups;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    private sealed record FicheTab(string Key, string Icon, string Label, int? Count);

    // Snapshot des champs journalisés dans l'onglet "Historique" (voir DiffFields). Même
    // principe que GlpiNg.Web.Components.Pages.Entities.Detail : peu importe quel bouton
    // "Sauvegarder" a été cliqué (onglet "Groupe" ou "Sécurité"), ce diff générique retrouve les
    // champs réellement modifiés sur le _group partagé en mémoire.
    private sealed record GroupSnapshot(
        string Name, string? Comment, string? Code, int? ParentId, bool IsRecursive,
        bool VisibleAsRequester, bool VisibleAsObserver, bool VisibleAsAssignee, bool VisibleAsTask,
        bool CanBeNotified, bool CanBeProjectSupervisor, bool CanContainItems, bool CanContainUsers,
        bool TwoFactorAuthRequired);

    [Parameter]
    public int GroupId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private GlpiNgDbContext? _db;
    private GlpiGroup? _group;
    private List<GlpiGroup> _allGroups = [];
    private Dictionary<int, GlpiGroup> _groupsById = [];
    private List<GlpiUser> _allUsers = [];
    private List<GlpiGroupNote> _notes = [];
    private List<GlpiGroupHistoryEntry> _historyEntries = [];
    private List<FicheTab> _tabs = [];
    private string _activeTabKey = "groupe";
    private bool _isSaving;
    private string _newSubgroupName = string.Empty;
    private string _newNoteContent = string.Empty;
    private int _userIdToAdd;
    private string _currentUserName = "Système";
    private GroupSnapshot _beforeEdit = null!;
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

        _group = await _db.Groups
            .Include(g => g.Children)
            .Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == GroupId);

        if (_group is null)
        {
            return;
        }

        await ReloadReferenceDataAsync();
        _beforeEdit = Snapshot(_group);
        RebuildTabs();

        _total = _allGroups.Count;
        _position = _allGroups.Count(g => g.Id <= GroupId);
        _previousId = _allGroups.Where(g => g.Id < GroupId).OrderByDescending(g => g.Id).Select(g => (int?)g.Id).FirstOrDefault();
        _nextId = _allGroups.Where(g => g.Id > GroupId).OrderBy(g => g.Id).Select(g => (int?)g.Id).FirstOrDefault();
    }

    private async Task ReloadReferenceDataAsync()
    {
        if (_db is null || _group is null)
        {
            return;
        }

        _allGroups = await _db.Groups.AsNoTracking().ToListAsync();
        _groupsById = _allGroups.ToDictionary(g => g.Id);
        _allUsers = await _db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .ToListAsync();

        _notes = await _db.GroupNotes.AsNoTracking()
            .Where(n => n.GroupId == _group.Id)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        _historyEntries = await _db.GroupHistoryEntries.AsNoTracking()
            .Where(h => h.GroupId == _group.Id)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
    }

    private void RebuildTabs()
    {
        if (_group is null)
        {
            return;
        }

        // Reprend la liste et l'ordre exacts des onglets de la fiche "Groupe" de GLPI (vérifiés
        // sur une instance GLPI réelle via front/group.form.php). "Notifications", "Tickets
        // créés", "Problèmes créés" et "Changements créés" dépendent d'un module Assistance
        // (ticketing/SLA) qui n'existe pas du tout dans GlpiNg. "Éléments gérés" nécessiterait de
        // relier un objet du parc (Computer, module Inventory) à un groupe propriétaire (module
        // Web) — un lien inter-modules qui n'existe pas encore (voir GlpiNg.Modules.Inventory.csproj
        // : le module Inventory ne dépend jamais de l'hôte). Les autres onglets ont un contenu
        // réel, voir le @switch de Detail.razor.
        _tabs =
        [
            new("groupe", "ti-users", "Groupe", null),
            new("subgroups", "ti-sitemap", "Sous-groupes", _group.Children.Count),
            new("elements", "ti-box", "Éléments gérés", null),
            new("security", "ti-shield", "Sécurité", null),
            new("users", "ti-user", "Utilisateurs", _group.Members.Count),
            new("notifications", "ti-bell", "Notifications", null),
            new("tickets", "ti-ticket", "Tickets créés", null),
            new("problems", "ti-alert-triangle", "Problèmes créés", null),
            new("changes", "ti-replace", "Changements créés", null),
            new("notes", "ti-notes", "Notes", _notes.Count),
            new("history", "ti-history", "Historique", _historyEntries.Count),
            new("all", "ti-list", "Tous", null),
        ];
    }

    // Messages explicites pour les onglets qui dépendent d'un module absent de GlpiNg, affichés
    // à la place du texte générique "n'est pas encore disponible" (voir Detail.razor).
    private static readonly Dictionary<string, string> UnavailableReasons = new()
    {
        ["elements"] = "Nécessite de relier les objets du parc (postes, etc.) à un groupe propriétaire — pas encore implémenté (le module Inventory ne dépend pas du module Web où vit GlpiGroup).",
        ["notifications"] = "Nécessite un moteur de notifications (modèles, transport mail) qui n'existe pas encore dans GlpiNg.",
        ["tickets"] = "Nécessite un module Assistance (tickets, SLA) qui n'existe pas encore dans GlpiNg.",
        ["problems"] = "Nécessite un module Assistance (problèmes) qui n'existe pas encore dans GlpiNg.",
        ["changes"] = "Nécessite un module Assistance (changements) qui n'existe pas encore dans GlpiNg.",
    };

    private void SetTab(string key)
    {
        _activeTabKey = key;
    }

    private static GroupSnapshot Snapshot(GlpiGroup g) => new(
        g.Name, g.Comment, g.Code, g.ParentId, g.IsRecursive,
        g.VisibleAsRequester, g.VisibleAsObserver, g.VisibleAsAssignee, g.VisibleAsTask,
        g.CanBeNotified, g.CanBeProjectSupervisor, g.CanContainItems, g.CanContainUsers,
        g.TwoFactorAuthRequired);

    private static string YesNo(bool value) => value ? Tr.T("Oui") : Tr.T("Non");

    private IEnumerable<(string Field, string? Old, string? New)> DiffFields(GroupSnapshot before, GroupSnapshot after)
    {
        if (before.Name != after.Name) yield return ("Nom", before.Name, after.Name);
        if (before.Comment != after.Comment) yield return ("Commentaires", before.Comment, after.Comment);
        if (before.Code != after.Code) yield return ("Code", before.Code, after.Code);
        if (before.ParentId != after.ParentId) yield return ("Comme enfant de", ParentLabel(before.ParentId), ParentLabel(after.ParentId));
        if (before.IsRecursive != after.IsRecursive) yield return ("Appartenance récursive", YesNo(before.IsRecursive), YesNo(after.IsRecursive));
        if (before.VisibleAsRequester != after.VisibleAsRequester) yield return ("Demandeur", YesNo(before.VisibleAsRequester), YesNo(after.VisibleAsRequester));
        if (before.VisibleAsObserver != after.VisibleAsObserver) yield return ("Observateur", YesNo(before.VisibleAsObserver), YesNo(after.VisibleAsObserver));
        if (before.VisibleAsAssignee != after.VisibleAsAssignee) yield return ("Attribué à", YesNo(before.VisibleAsAssignee), YesNo(after.VisibleAsAssignee));
        if (before.VisibleAsTask != after.VisibleAsTask) yield return ("Tâche", YesNo(before.VisibleAsTask), YesNo(after.VisibleAsTask));
        if (before.CanBeNotified != after.CanBeNotified) yield return ("Peut être notifié", YesNo(before.CanBeNotified), YesNo(after.CanBeNotified));
        if (before.CanBeProjectSupervisor != after.CanBeProjectSupervisor) yield return ("Peut être superviseur", YesNo(before.CanBeProjectSupervisor), YesNo(after.CanBeProjectSupervisor));
        if (before.CanContainItems != after.CanContainItems) yield return ("Peut contenir des éléments", YesNo(before.CanContainItems), YesNo(after.CanContainItems));
        if (before.CanContainUsers != after.CanContainUsers) yield return ("Peut contenir des utilisateurs", YesNo(before.CanContainUsers), YesNo(after.CanContainUsers));
        if (before.TwoFactorAuthRequired != after.TwoFactorAuthRequired) yield return ("Forcer 2FA", YesNo(before.TwoFactorAuthRequired), YesNo(after.TwoFactorAuthRequired));
    }

    private static string FormatChange(string? oldValue, string? newValue) =>
        $"{(string.IsNullOrEmpty(oldValue) ? "vide" : oldValue)} → {(string.IsNullOrEmpty(newValue) ? "vide" : newValue)}";

    private async Task SaveAsync()
    {
        if (_db is null || _group is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            GroupSnapshot after = Snapshot(_group);
            List<GlpiGroupHistoryEntry> entries = DiffFields(_beforeEdit, after)
                .Select(diff => new GlpiGroupHistoryEntry
                {
                    GroupId = _group.Id,
                    User = _currentUserName,
                    Field = diff.Field,
                    Description = FormatChange(diff.Old, diff.New)
                })
                .ToList();

            _group.UpdatedAt = DateTime.UtcNow;
            if (entries.Count > 0)
            {
                _db.GroupHistoryEntries.AddRange(entries);
            }

            await _db.SaveChangesAsync();
            _beforeEdit = after;
            await ReloadReferenceDataAsync();
            RebuildTabs();
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Groupe enregistré.")));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _group is null)
        {
            return;
        }

        if (_group.Children.Count > 0)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Impossible de supprimer un groupe qui a des sous-groupes : détachez-les d'abord.")));
            return;
        }

        _db.GroupUsers.RemoveRange(_group.Members);
        _db.Groups.Remove(_group);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/admin/groups");
    }

    private IEnumerable<GlpiGroup> ParentCandidates()
    {
        if (_group is null)
        {
            yield break;
        }

        HashSet<int> excluded = DescendantIds(_group.Id);
        foreach (GlpiGroup candidate in _allGroups.Where(g => !excluded.Contains(g.Id)).OrderBy(FullName))
        {
            yield return candidate;
        }
    }

    private HashSet<int> DescendantIds(int rootId)
    {
        var result = new HashSet<int> { rootId };
        var queue = new Queue<int>();
        queue.Enqueue(rootId);

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();
            foreach (GlpiGroup child in _allGroups.Where(g => g.ParentId == current))
            {
                if (result.Add(child.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    private string ParentLabel(int? parentId) =>
        parentId is { } id && _groupsById.TryGetValue(id, out GlpiGroup? parent) ? parent.Name : "-----";

    private string FullName(GlpiGroup group)
    {
        var parts = new List<string> { group.Name };
        GlpiGroup current = group;

        while (current.ParentId is { } parentId && _groupsById.TryGetValue(parentId, out var parent))
        {
            parts.Insert(0, parent.Name);
            current = parent;
        }

        return string.Join(" > ", parts);
    }

    private async Task AddSubgroupAsync()
    {
        if (_db is null || _group is null || string.IsNullOrWhiteSpace(_newSubgroupName))
        {
            return;
        }

        var child = new GlpiGroup { Name = _newSubgroupName.Trim(), ParentId = _group.Id };
        _db.Groups.Add(child);
        await _db.SaveChangesAsync();

        if (!_group.Children.Contains(child))
        {
            _group.Children.Add(child);
        }

        await ReloadReferenceDataAsync();
        _newSubgroupName = string.Empty;
        RebuildTabs();
    }

    private IEnumerable<GlpiUser> AvailableUsers()
    {
        if (_group is null)
        {
            return _allUsers;
        }

        HashSet<int> memberIds = _group.Members.Select(m => m.UserId).ToHashSet();
        return _allUsers.Where(u => !memberIds.Contains(u.Id));
    }

    // Recharge Members depuis la base (avec Include(User)) plutôt que de patcher la collection en
    // mémoire : _group est suivi par le change tracker, et EF fait du "fixup" automatique de
    // Members dès l'Add/Remove ci-dessous (avant même SaveChangesAsync), avec une entrée dont
    // .User n'est pas encore renseigné — d'où un NullReferenceException au rendu si on ne
    // recharge pas explicitement après coup.
    private async Task ReloadMembersAsync()
    {
        if (_db is null || _group is null)
        {
            return;
        }

        await _db.Entry(_group).Collection(g => g.Members).Query().Include(m => m.User).LoadAsync();
    }

    private async Task AddUserAsync()
    {
        if (_db is null || _group is null || _userIdToAdd == 0)
        {
            return;
        }

        _db.GroupUsers.Add(new GlpiGroupUser { GroupId = _group.Id, UserId = _userIdToAdd });
        await _db.SaveChangesAsync();
        await ReloadMembersAsync();

        _userIdToAdd = 0;
        RebuildTabs();
    }

    private async Task RemoveUserAsync(int userId)
    {
        if (_db is null || _group is null)
        {
            return;
        }

        GlpiGroupUser? membership = _group.Members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
        {
            return;
        }

        _db.GroupUsers.Remove(membership);
        await _db.SaveChangesAsync();
        await ReloadMembersAsync();

        RebuildTabs();
    }

    private async Task AddNoteAsync()
    {
        if (_db is null || _group is null || string.IsNullOrWhiteSpace(_newNoteContent))
        {
            return;
        }

        var note = new GlpiGroupNote
        {
            GroupId = _group.Id,
            Content = _newNoteContent.Trim(),
            Author = _currentUserName
        };
        _db.GroupNotes.Add(note);
        await _db.SaveChangesAsync();

        _newNoteContent = string.Empty;
        await ReloadReferenceDataAsync();
        RebuildTabs();
    }

    private async Task DeleteNoteAsync(int noteId)
    {
        if (_db is null)
        {
            return;
        }

        GlpiGroupNote? note = await _db.GroupNotes.FirstOrDefaultAsync(n => n.Id == noteId);
        if (note is null)
        {
            return;
        }

        _db.GroupNotes.Remove(note);
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
