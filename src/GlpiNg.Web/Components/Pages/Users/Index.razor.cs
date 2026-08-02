using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Users;

public partial class Index : ComponentBase
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    private List<GlpiUser> _users = [];
    private List<GlpiUser> _filteredUsers = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private GlpiUser _newUser = NewBlankUser();
    private string _newPassword = string.Empty;
    private string _newPasswordConfirm = string.Empty;
    private string? _createError;

    private bool AllSelected => _filteredUsers.Count > 0 && _selectedIds.Count == _filteredUsers.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newUser.UserName)
        && !string.IsNullOrEmpty(_newPassword)
        && _newPassword == _newPasswordConfirm;

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _users = await db.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync()
    {
        await LoadAsync();
    }

    private void OnSearchChanged(KeyboardEventArgs args)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<GlpiUser> query = term.Length == 0
            ? _users
            : _users.Where(user => MatchesSearch(user, term));

        _filteredUsers = query.OrderBy(user => user.UserName).ToList();
        _selectedIds.IntersectWith(_filteredUsers.Select(user => user.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (GlpiUser user in _filteredUsers)
            {
                _selectedIds.Add(user.Id);
            }
        }
    }

    private void ToggleSelect(int userId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(userId);
        }
        else
        {
            _selectedIds.Remove(userId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0) return;

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<GlpiUser> toDelete = await db.Users
            .Where(user => _selectedIds.Contains(user.Id))
            .ToListAsync();

        db.Users.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateUserAsync()
    {
        if (!CanCreate)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        bool alreadyExists = await db.Users.AsNoTracking().AnyAsync(u => u.UserName == _newUser.UserName);
        if (alreadyExists)
        {
            _createError = "Cet identifiant est déjà utilisé.";
            return;
        }

        _newUser.PasswordHash = Hasher.HashPassword(_newUser, _newPassword);
        _newUser.DisplayName = ComputeDisplayName(_newUser);
        db.Users.Add(_newUser);
        await db.SaveChangesAsync();

        _newUser = NewBlankUser();
        _newPassword = string.Empty;
        _newPasswordConfirm = string.Empty;
        _createError = null;

        await JS.InvokeVoidAsync("glpiNg.hideModal", "newUserModal");
        await LoadAsync();
    }

    private static GlpiUser NewBlankUser() => new() { UserName = string.Empty, PasswordHash = string.Empty };

    private static bool MatchesSearch(GlpiUser user, string term)
    {
        return user.UserName.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (user.FirstName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (user.LastName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (user.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (user.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>GLPI calcule le nom complet à partir de Nom/Prénom plutôt que de le laisser saisir séparément.</summary>
    private static string ComputeDisplayName(GlpiUser user)
    {
        string fullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return fullName.Length > 0 ? fullName : user.UserName;
    }
}
