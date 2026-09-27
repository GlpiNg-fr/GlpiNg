using GlpiNg.Modules.Abstractions.Localization;
﻿using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
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

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private LdapUserImportService LdapImport { get; set; } = null!;

    private List<GlpiUser> _users = [];
    private List<GlpiUser> _filteredUsers = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private GlpiUser _newUser = NewBlankUser();
    private string _newPassword = string.Empty;
    private string _newPasswordConfirm = string.Empty;

    // --- Ajout depuis une source externe (annuaire LDAP)
    private List<AuthLdapServer> _ldapServers = [];
    private int _importServerId;
    private string _importTerm = string.Empty;
    private List<LdapImportCandidate> _ldapCandidates = [];
    private readonly HashSet<string> _selectedLogins = [];
    private string? _ldapImportError;
    private bool _isSearchingLdap;
    private bool _isImportingLdap;
    private bool _hasSearchedLdap;

    private bool AllSelected => _filteredUsers.Count > 0 && _selectedIds.Count == _filteredUsers.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newUser.UserName)
        && !string.IsNullOrEmpty(_newPassword)
        && _newPassword == _newPasswordConfirm;

    protected override async Task OnInitializedAsync()
    {
        await LoadLdapServersAsync();
        await LoadAsync();
    }

    /// <summary>
    /// Annuaires proposés à l'import : uniquement les actifs, comme pour l'authentification. Un
    /// annuaire désactivé ne doit pas servir de source.
    /// </summary>
    private async Task LoadLdapServersAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _ldapServers = await db.AuthLdapServers.AsNoTracking()
            .Where(server => server.IsActive)
            .OrderByDescending(server => server.IsDefault)
            .ThenBy(server => server.Name)
            .ToListAsync();

        _importServerId = _ldapServers.FirstOrDefault()?.Id ?? 0;
    }

    private async Task OpenImportAsync()
    {
        _ldapCandidates = [];
        _selectedLogins.Clear();
        _ldapImportError = null;
        _hasSearchedLdap = false;
        _importTerm = string.Empty;

        // Rechargé à l'ouverture : un annuaire a pu être ajouté ou désactivé depuis l'affichage
        // de la page.
        await LoadLdapServersAsync();
        await JS.InvokeVoidAsync("glping.showModal", "ldapImportModal");
    }

    private async Task SearchLdapAsync()
    {
        if (_importServerId == 0)
        {
            return;
        }

        _isSearchingLdap = true;
        _ldapImportError = null;
        _selectedLogins.Clear();

        try
        {
            (IReadOnlyList<LdapImportCandidate> candidates, string? error) =
                await LdapImport.SearchAsync(_importServerId, _importTerm);

            _ldapCandidates = [.. candidates];
            _ldapImportError = error;
            _hasSearchedLdap = true;
        }
        finally
        {
            _isSearchingLdap = false;
        }
    }

    /// <summary>Candidats que l'import peut effectivement traiter : ceux déjà présents dans GlpiNg
    /// sont exclus, leur case étant désactivée dans le tableau.</summary>
    private IEnumerable<LdapImportCandidate> ImportableCandidates =>
        _ldapCandidates.Where(candidate => !candidate.AlreadyPresent);

    private bool AllCandidatesSelected => ImportableCandidates.Any()
        && _selectedLogins.IsSupersetOf(ImportableCandidates.Select(candidate => candidate.Login));

    private void ToggleSelectAllCandidates(bool selectAll)
    {
        foreach (LdapImportCandidate candidate in ImportableCandidates)
        {
            if (selectAll)
            {
                _selectedLogins.Add(candidate.Login);
            }
            else
            {
                _selectedLogins.Remove(candidate.Login);
            }
        }
    }

    private void ToggleLogin(string login, bool selected)
    {
        if (selected) _selectedLogins.Add(login);
        else _selectedLogins.Remove(login);
    }

    private async Task ImportLdapUsersAsync()
    {
        if (_selectedLogins.Count == 0)
        {
            return;
        }

        _isImportingLdap = true;

        try
        {
            LdapImportResult result = await LdapImport.ImportAsync(_importServerId, [.. _selectedLogins]);

            ToastService.Notify(new ToastMessage(
                result.Created > 0 ? ToastType.Success : ToastType.Warning,
                $"{result.Created} compte(s) importé(s)" + (result.Skipped > 0 ? Tr.T(", {0} ignoré(s).", result.Skipped) : ".")));

            foreach (string warning in result.Warnings)
            {
                ToastService.Notify(new ToastMessage(ToastType.Warning, warning));
            }

            await JS.InvokeVoidAsync("glping.hideModal", "ldapImportModal");
            await LoadAsync();
        }
        finally
        {
            _isImportingLdap = false;
        }
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
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Cet identifiant est déjà utilisé.")));
            return;
        }

        _newUser.PasswordHash = Hasher.HashPassword(_newUser, _newPassword);
        _newUser.DisplayName = ComputeDisplayName(_newUser);
        db.Users.Add(_newUser);
        await db.SaveChangesAsync();

        _newUser = NewBlankUser();
        _newPassword = string.Empty;
        _newPasswordConfirm = string.Empty;

        await JS.InvokeVoidAsync("glping.hideModal", "newUserModal");
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
