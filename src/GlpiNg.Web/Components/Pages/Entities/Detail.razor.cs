using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Entities;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    private sealed record FicheTab(string Key, string Icon, string Label, int? Count);

    // Snapshot des champs journalisés dans l'onglet "Historique" (voir DiffFields). Couvre les
    // onglets "entite", "adresse" et les 4 onglets de paramétrage ("avance", "securite", "parc",
    // "interface") qui mutent tous le même _entity en mémoire : peu importe quel bouton
    // "Sauvegarder" a été cliqué, ce diff générique retrouve les champs réellement modifiés.
    private sealed record EntitySnapshot(
        string Name, string? Comment, int? ParentId,
        string? Address, string? Postcode, string? Town, string? State, string? Country,
        string? Website, string? Phone, string? Fax, string? Email, string? Registration,
        string? Longitude, string? Latitude, string? Altitude,
        string? AssignmentTag, string? AssignmentEmailDomain, bool TwoFactorAuthRequired,
        string? AgentBaseUrl, bool CustomCssEnabled, string? CustomCss);

    [Parameter]
    public int EntityId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private GlpiNgDbContext? _db;
    private GlpiEntity? _entity;
    private List<GlpiEntity> _allEntities = [];
    private Dictionary<int, GlpiEntity> _entitiesById = [];
    private List<GlpiUser> _allUsers = [];
    private List<GlpiProfile> _allProfiles = [];
    private List<GlpiUserProfile> _entityHabilitations = [];
    private List<GlpiEntityNote> _notes = [];
    private List<GlpiEntityHistoryEntry> _historyEntries = [];
    private List<FicheTab> _tabs = [];
    private string _activeTabKey = "entite";
    private string? _statusMessage;
    private bool _isSaving;
    private string _newSubEntityName = string.Empty;
    private string _newNoteContent = string.Empty;
    private int _userIdToAssign;
    private int _profileIdToAssign;
    private bool _isRecursiveToAssign;
    private string _currentUserName = "Système";
    private EntitySnapshot _beforeEdit = null!;
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

        _entity = await _db.Entities
            .Include(e => e.Children)
            .FirstOrDefaultAsync(e => e.Id == EntityId);

        if (_entity is null)
        {
            return;
        }

        await ReloadReferenceDataAsync();
        _beforeEdit = Snapshot(_entity);
        RebuildTabs();

        _total = _allEntities.Count;
        _position = _allEntities.Count(e => e.Id <= EntityId);
        _previousId = _allEntities.Where(e => e.Id < EntityId).OrderByDescending(e => e.Id).Select(e => (int?)e.Id).FirstOrDefault();
        _nextId = _allEntities.Where(e => e.Id > EntityId).OrderBy(e => e.Id).Select(e => (int?)e.Id).FirstOrDefault();
    }

    private async Task ReloadReferenceDataAsync()
    {
        if (_db is null || _entity is null)
        {
            return;
        }

        _allEntities = await _db.Entities.AsNoTracking().ToListAsync();
        _entitiesById = _allEntities.ToDictionary(e => e.Id);

        _allUsers = await _db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .ToListAsync();
        _allProfiles = await _db.Profiles.AsNoTracking().OrderBy(p => p.Name).ToListAsync();

        _entityHabilitations = await _db.UserProfiles.AsNoTracking()
            .Include(h => h.User)
            .Include(h => h.Profile)
            .Where(h => h.EntityId == _entity.Id)
            .ToListAsync();

        _notes = await _db.EntityNotes.AsNoTracking()
            .Where(n => n.EntityId == _entity.Id)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        _historyEntries = await _db.EntityHistoryEntries.AsNoTracking()
            .Where(h => h.EntityId == _entity.Id)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
    }

    private void RebuildTabs()
    {
        if (_entity is null)
        {
            return;
        }

        // Reprend la liste et l'ordre exacts des onglets de la fiche "Entité" de GLPI (vérifiés
        // sur une instance GLPI réelle via front/entity.form.php). "Notifications", "Assistance",
        // "Accueil Helpdesk", "Règles", "Documents" et "Base de connaissances" dépendent de
        // modules entiers qui n'existent pas du tout dans GlpiNg (moteur de notifications,
        // ticketing/SLA, portail libre-service, moteur de règles, gestion documentaire, KB) : ce
        // sont des placeholders explicites plutôt que des formulaires factices. Les autres
        // onglets ont un contenu réel, voir le @switch de Detail.razor.
        _tabs =
        [
            new("entite", "ti-sitemap", "Entité", null),
            new("sous-entites", "ti-hierarchy", "Entités", _entity.Children.Count),
            new("adresse", "ti-map-pin", "Adresse", null),
            new("avance", "ti-settings", "Informations avancées", null),
            new("notifications", "ti-bell", "Notifications", null),
            new("assistance", "ti-headset", "Assistance", null),
            new("parc", "ti-box", "Parc", null),
            new("interface", "ti-layout", "Personnalisation de l'interface", null),
            new("securite", "ti-shield", "Sécurité", null),
            new("helpdesk", "ti-lifebuoy", "Accueil Helpdesk", null),
            new("utilisateurs", "ti-user", "Utilisateurs", _entityHabilitations.Count),
            new("regles", "ti-adjustments", "Règles", null),
            new("documents", "ti-file", "Documents", null),
            new("notes", "ti-notes", "Notes", _notes.Count),
            new("kb", "ti-book", "Base de connaissances", null),
            new("history", "ti-history", "Historique", _historyEntries.Count),
            new("all", "ti-list", "Tous", null),
        ];
    }

    // Messages explicites pour les onglets qui dépendent d'un module absent de GlpiNg, affichés
    // à la place du texte générique "n'est pas encore disponible" (voir Detail.razor).
    private static readonly Dictionary<string, string> UnavailableReasons = new()
    {
        ["notifications"] = "Nécessite un moteur de notifications (modèles, transport mail) qui n'existe pas encore dans GlpiNg.",
        ["assistance"] = "Nécessite un module Assistance (tickets, SLA, catégories) qui n'existe pas encore dans GlpiNg.",
        ["helpdesk"] = "Nécessite un portail libre-service (tuiles, catalogue de services) qui n'existe pas encore dans GlpiNg.",
        ["regles"] = "Nécessite un moteur de règles métier qui n'existe pas encore dans GlpiNg.",
        ["documents"] = "Nécessite un module de gestion documentaire qui n'existe pas encore dans GlpiNg.",
        ["kb"] = "Nécessite une base de connaissances qui n'existe pas encore dans GlpiNg.",
    };

    private void SetTab(string key)
    {
        _activeTabKey = key;
    }

    private static EntitySnapshot Snapshot(GlpiEntity e) => new(
        e.Name, e.Comment, e.ParentId,
        e.Address, e.Postcode, e.Town, e.State, e.Country,
        e.Website, e.Phone, e.Fax, e.Email, e.Registration,
        e.Longitude, e.Latitude, e.Altitude,
        e.AssignmentTag, e.AssignmentEmailDomain, e.TwoFactorAuthRequired,
        e.AgentBaseUrl, e.CustomCssEnabled, e.CustomCss);

    private string ParentLabel(int? parentId) =>
        parentId is { } id && _entitiesById.TryGetValue(id, out GlpiEntity? parent) ? parent.Name : "-----";

    private static string YesNo(bool value) => value ? "Oui" : "Non";

    private IEnumerable<(string Field, string? Old, string? New)> DiffFields(EntitySnapshot before, EntitySnapshot after)
    {
        if (before.Name != after.Name) yield return ("Nom", before.Name, after.Name);
        if (before.Comment != after.Comment) yield return ("Commentaires", before.Comment, after.Comment);
        if (before.ParentId != after.ParentId) yield return ("Entité parente", ParentLabel(before.ParentId), ParentLabel(after.ParentId));
        if (before.Address != after.Address) yield return ("Adresse", before.Address, after.Address);
        if (before.Postcode != after.Postcode) yield return ("Code postal", before.Postcode, after.Postcode);
        if (before.Town != after.Town) yield return ("Ville", before.Town, after.Town);
        if (before.State != after.State) yield return ("État", before.State, after.State);
        if (before.Country != after.Country) yield return ("Pays", before.Country, after.Country);
        if (before.Website != after.Website) yield return ("Site Web", before.Website, after.Website);
        if (before.Phone != after.Phone) yield return ("Téléphone", before.Phone, after.Phone);
        if (before.Fax != after.Fax) yield return ("Fax", before.Fax, after.Fax);
        if (before.Email != after.Email) yield return ("E-mail", before.Email, after.Email);
        if (before.Registration != after.Registration) yield return ("Matricule", before.Registration, after.Registration);
        if (before.Longitude != after.Longitude) yield return ("Longitude", before.Longitude, after.Longitude);
        if (before.Latitude != after.Latitude) yield return ("Latitude", before.Latitude, after.Latitude);
        if (before.Altitude != after.Altitude) yield return ("Altitude", before.Altitude, after.Altitude);
        if (before.AssignmentTag != after.AssignmentTag) yield return ("Balise (TAG) de l'outil d'inventaire", before.AssignmentTag, after.AssignmentTag);
        if (before.AssignmentEmailDomain != after.AssignmentEmailDomain) yield return ("Domaine de messagerie", before.AssignmentEmailDomain, after.AssignmentEmailDomain);
        if (before.TwoFactorAuthRequired != after.TwoFactorAuthRequired) yield return ("Forcer 2FA", YesNo(before.TwoFactorAuthRequired), YesNo(after.TwoFactorAuthRequired));
        if (before.AgentBaseUrl != after.AgentBaseUrl) yield return ("URL Base pour l'agent", before.AgentBaseUrl, after.AgentBaseUrl);
        if (before.CustomCssEnabled != after.CustomCssEnabled) yield return ("Personnalisation CSS activée", YesNo(before.CustomCssEnabled), YesNo(after.CustomCssEnabled));
        if (before.CustomCss != after.CustomCss) yield return ("CSS personnalisé", before.CustomCss, after.CustomCss);
    }

    private static string FormatChange(string? oldValue, string? newValue) =>
        $"{(string.IsNullOrEmpty(oldValue) ? "vide" : oldValue)} → {(string.IsNullOrEmpty(newValue) ? "vide" : newValue)}";

    private async Task SaveAsync()
    {
        if (_db is null || _entity is null)
        {
            return;
        }

        _isSaving = true;
        _statusMessage = null;

        try
        {
            EntitySnapshot after = Snapshot(_entity);
            List<GlpiEntityHistoryEntry> entries = DiffFields(_beforeEdit, after)
                .Select(diff => new GlpiEntityHistoryEntry
                {
                    EntityId = _entity.Id,
                    User = _currentUserName,
                    Field = diff.Field,
                    Description = FormatChange(diff.Old, diff.New)
                })
                .ToList();

            _entity.UpdatedAt = DateTime.UtcNow;
            if (entries.Count > 0)
            {
                _db.EntityHistoryEntries.AddRange(entries);
            }

            await _db.SaveChangesAsync();
            _beforeEdit = after;
            await ReloadReferenceDataAsync();
            RebuildTabs();
            _statusMessage = "Entité enregistrée.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _entity is null)
        {
            return;
        }

        if (_entity.Children.Count > 0)
        {
            _statusMessage = "Impossible de supprimer une entité qui a des sous-entités : détachez-les d'abord.";
            return;
        }

        if (_entityHabilitations.Count > 0)
        {
            _statusMessage = "Impossible de supprimer une entité à laquelle des utilisateurs sont rattachés : détachez-les d'abord.";
            return;
        }

        _db.Entities.Remove(_entity);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/entities");
    }

    private IEnumerable<GlpiEntity> ParentCandidates()
    {
        if (_entity is null)
        {
            yield break;
        }

        HashSet<int> excluded = DescendantIds(_entity.Id);
        foreach (GlpiEntity candidate in _allEntities.Where(e => !excluded.Contains(e.Id)).OrderBy(FullName))
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
            foreach (GlpiEntity child in _allEntities.Where(e => e.ParentId == current))
            {
                if (result.Add(child.Id))
                {
                    queue.Enqueue(child.Id);
                }
            }
        }

        return result;
    }

    private string FullName(GlpiEntity entity)
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

    private async Task AddSubEntityAsync()
    {
        if (_db is null || _entity is null || string.IsNullOrWhiteSpace(_newSubEntityName))
        {
            return;
        }

        var child = new GlpiEntity { Name = _newSubEntityName.Trim(), ParentId = _entity.Id };
        _db.Entities.Add(child);
        await _db.SaveChangesAsync();

        if (!_entity.Children.Contains(child))
        {
            _entity.Children.Add(child);
        }

        await ReloadReferenceDataAsync();
        _newSubEntityName = string.Empty;
        RebuildTabs();
    }

    private async Task AddHabilitationAsync()
    {
        if (_db is null || _entity is null || _userIdToAssign == 0 || _profileIdToAssign == 0)
        {
            return;
        }

        _db.UserProfiles.Add(new GlpiUserProfile
        {
            UserId = _userIdToAssign,
            EntityId = _entity.Id,
            ProfileId = _profileIdToAssign,
            IsRecursive = _isRecursiveToAssign
        });
        await _db.SaveChangesAsync();

        _userIdToAssign = 0;
        _profileIdToAssign = 0;
        _isRecursiveToAssign = false;
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

    private async Task AddNoteAsync()
    {
        if (_db is null || _entity is null || string.IsNullOrWhiteSpace(_newNoteContent))
        {
            return;
        }

        var note = new GlpiEntityNote
        {
            EntityId = _entity.Id,
            Content = _newNoteContent.Trim(),
            Author = _currentUserName
        };
        _db.EntityNotes.Add(note);
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

        GlpiEntityNote? note = await _db.EntityNotes.FirstOrDefaultAsync(n => n.Id == noteId);
        if (note is null)
        {
            return;
        }

        _db.EntityNotes.Remove(note);
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
