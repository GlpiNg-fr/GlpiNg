using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Profiles;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    private sealed record FicheTab(string Key, string Icon, string Label, int? Count);

    // Snapshot des champs journalisés dans l'onglet "Historique" (voir DiffFields). Même
    // principe que les autres fiches (Entité/Groupe/Utilisateur) : peu importe quel bouton
    // "Sauvegarder" a été cliqué (onglet "Profil" ou "Droits"), ce diff générique retrouve les
    // champs réellement modifiés sur le _profile partagé en mémoire.
    private sealed record ProfileSnapshot(
        string Name, string? Comment, bool IsDefault,
        ProfileRightLevel ParcRight, ProfileRightLevel AssistanceRight, ProfileRightLevel GestionRight,
        ProfileRightLevel OutilsRight, ProfileRightLevel AdministrationRight, ProfileRightLevel ConfigurationRight);

    [Parameter]
    public int ProfileId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private GlpiNgDbContext? _db;
    private GlpiProfile? _profile;
    private List<GlpiUser> _allUsers = [];
    private List<GlpiEntity> _allEntities = [];
    private List<GlpiUserProfile> _profileHabilitations = [];
    private List<GlpiProfileHistoryEntry> _historyEntries = [];
    private List<FicheTab> _tabs = [];
    private string _activeTabKey = "profil";
    private string? _statusMessage;
    private bool _isSaving;
    private int _userIdToAssign;
    private int _entityIdToAssign;
    private bool _isRecursiveToAssign;
    private string _currentUserName = "Système";
    private ProfileSnapshot _beforeEdit = null!;
    private int _position;
    private int _total;
    private int? _previousId;
    private int? _nextId;
    private List<GlpiProfile> _allProfiles = [];

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

        _profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == ProfileId);

        if (_profile is null)
        {
            return;
        }

        await ReloadReferenceDataAsync();
        _beforeEdit = Snapshot(_profile);
        RebuildTabs();

        _total = _allProfiles.Count;
        _position = _allProfiles.Count(p => p.Id <= ProfileId);
        _previousId = _allProfiles.Where(p => p.Id < ProfileId).OrderByDescending(p => p.Id).Select(p => (int?)p.Id).FirstOrDefault();
        _nextId = _allProfiles.Where(p => p.Id > ProfileId).OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefault();
    }

    private async Task ReloadReferenceDataAsync()
    {
        if (_db is null || _profile is null)
        {
            return;
        }

        _allProfiles = await _db.Profiles.AsNoTracking().ToListAsync();

        _allUsers = await _db.Users.AsNoTracking()
            .OrderBy(u => u.DisplayName ?? u.UserName)
            .ToListAsync();
        _allEntities = await _db.Entities.AsNoTracking().ToListAsync();

        _profileHabilitations = await _db.UserProfiles.AsNoTracking()
            .Include(h => h.User)
            .Include(h => h.Entity)
            .Where(h => h.ProfileId == _profile.Id)
            .ToListAsync();

        _historyEntries = await _db.ProfileHistoryEntries.AsNoTracking()
            .Where(h => h.ProfileId == _profile.Id)
            .OrderByDescending(h => h.OccurredAt)
            .ToListAsync();
    }

    private void RebuildTabs()
    {
        if (_profile is null)
        {
            return;
        }

        // Contrairement aux fiches Entité/Groupe/Utilisateur, la fiche "Profil" n'a pas de liste
        // d'onglets GLPI à reproduire ici : GLPI y expose une matrice de droits très fine par
        // module sur des dizaines de sous-onglets, alors que GlpiNg simplifie volontairement à un
        // seul droit par grande section du menu (voir GlpiProfile). Les onglets ci-dessous
        // couvrent donc l'intégralité de ce que cette version simplifiée gère, tous avec un
        // contenu réel (voir le @switch de Detail.razor).
        _tabs =
        [
            new("profil", "ti-id-badge", "Profil", null),
            new("droits", "ti-lock", "Droits", null),
            new("utilisateurs", "ti-user", "Utilisateurs", _profileHabilitations.Count),
            new("history", "ti-history", "Historique", _historyEntries.Count),
            new("all", "ti-list", "Tous", null),
        ];
    }

    private void SetTab(string key)
    {
        _activeTabKey = key;
    }

    private static ProfileSnapshot Snapshot(GlpiProfile p) => new(
        p.Name, p.Comment, p.IsDefault,
        p.ParcRight, p.AssistanceRight, p.GestionRight,
        p.OutilsRight, p.AdministrationRight, p.ConfigurationRight);

    private static string YesNo(bool value) => value ? "Oui" : "Non";

    private static string RightLabel(ProfileRightLevel level) => level switch
    {
        ProfileRightLevel.None => "Aucun accès",
        ProfileRightLevel.Read => "Lecture",
        ProfileRightLevel.Write => "Lecture / Écriture",
        _ => level.ToString()
    };

    private IEnumerable<(string Field, string? Old, string? New)> DiffFields(ProfileSnapshot before, ProfileSnapshot after)
    {
        if (before.Name != after.Name) yield return ("Nom", before.Name, after.Name);
        if (before.Comment != after.Comment) yield return ("Commentaires", before.Comment, after.Comment);
        if (before.IsDefault != after.IsDefault) yield return ("Profil par défaut", YesNo(before.IsDefault), YesNo(after.IsDefault));
        if (before.ParcRight != after.ParcRight) yield return ("Droit — Parc", RightLabel(before.ParcRight), RightLabel(after.ParcRight));
        if (before.AssistanceRight != after.AssistanceRight) yield return ("Droit — Assistance", RightLabel(before.AssistanceRight), RightLabel(after.AssistanceRight));
        if (before.GestionRight != after.GestionRight) yield return ("Droit — Gestion", RightLabel(before.GestionRight), RightLabel(after.GestionRight));
        if (before.OutilsRight != after.OutilsRight) yield return ("Droit — Outils", RightLabel(before.OutilsRight), RightLabel(after.OutilsRight));
        if (before.AdministrationRight != after.AdministrationRight) yield return ("Droit — Administration", RightLabel(before.AdministrationRight), RightLabel(after.AdministrationRight));
        if (before.ConfigurationRight != after.ConfigurationRight) yield return ("Droit — Configuration", RightLabel(before.ConfigurationRight), RightLabel(after.ConfigurationRight));
    }

    private static string FormatChange(string? oldValue, string? newValue) =>
        $"{(string.IsNullOrEmpty(oldValue) ? "vide" : oldValue)} → {(string.IsNullOrEmpty(newValue) ? "vide" : newValue)}";

    private async Task SaveAsync()
    {
        if (_db is null || _profile is null)
        {
            return;
        }

        _isSaving = true;
        _statusMessage = null;

        try
        {
            ProfileSnapshot after = Snapshot(_profile);
            List<GlpiProfileHistoryEntry> entries = DiffFields(_beforeEdit, after)
                .Select(diff => new GlpiProfileHistoryEntry
                {
                    ProfileId = _profile.Id,
                    User = _currentUserName,
                    Field = diff.Field,
                    Description = FormatChange(diff.Old, diff.New)
                })
                .ToList();

            _profile.UpdatedAt = DateTime.UtcNow;
            if (entries.Count > 0)
            {
                _db.ProfileHistoryEntries.AddRange(entries);
            }

            await _db.SaveChangesAsync();
            _beforeEdit = after;
            await ReloadReferenceDataAsync();
            RebuildTabs();
            _statusMessage = "Profil enregistré.";
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _profile is null)
        {
            return;
        }

        if (_profileHabilitations.Count > 0)
        {
            _statusMessage = "Impossible de supprimer un profil auquel des utilisateurs sont rattachés : détachez-les d'abord.";
            return;
        }

        _db.Profiles.Remove(_profile);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/profiles");
    }

    private async Task AddHabilitationAsync()
    {
        if (_db is null || _profile is null || _userIdToAssign == 0 || _entityIdToAssign == 0)
        {
            return;
        }

        _db.UserProfiles.Add(new GlpiUserProfile
        {
            UserId = _userIdToAssign,
            EntityId = _entityIdToAssign,
            ProfileId = _profile.Id,
            IsRecursive = _isRecursiveToAssign
        });
        await _db.SaveChangesAsync();

        _userIdToAssign = 0;
        _entityIdToAssign = 0;
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

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
