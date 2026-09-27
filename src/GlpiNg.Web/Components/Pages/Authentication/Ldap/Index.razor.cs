using GlpiNg.Modules.Abstractions.Localization;
﻿using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.Authentication.Ldap;

public partial class Index : ComponentBase
{
    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private AuthSecretProtector Protector { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    private List<AuthLdapServer> _servers = [];
    private List<AuthLdapServer> _filteredServers = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private AuthLdapServer _newServer = NewBlankServer();
    private string _newBindPassword = string.Empty;

    private bool AllSelected => _filteredServers.Count > 0 && _selectedIds.Count == _filteredServers.Count;

    private bool CanCreate => !string.IsNullOrWhiteSpace(_newServer.Name)
        && !string.IsNullOrWhiteSpace(_newServer.Host)
        && !string.IsNullOrWhiteSpace(_newServer.BaseDn);

    protected override async Task OnInitializedAsync()
    {
        AuthSettings authSettings = await SettingsStore.ReadSectionAsync<AuthSettings>("AuthSettings");
        _autoAddExternalUsers = authSettings.AutoAddUsersFromExternalAuth;

        GeneralSettings general = await SettingsStore.ReadSectionAsync<GeneralSettings>("GeneralSettings");
        _showAuthSourcesOnLoginPage = general.ShowAuthSourcesOnLoginPage;

        await LoadAsync();
    }

    /// <summary>Réglages dont dépend l'utilisabilité d'un annuaire, lus pour les rappels affichés en tête de page.</summary>
    private bool _autoAddExternalUsers;
    private bool _showAuthSourcesOnLoginPage;

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _servers = await db.AuthLdapServers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private async Task OnRefreshAsync() => await LoadAsync();

    private void OnSearchChanged(KeyboardEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        IEnumerable<AuthLdapServer> query = term.Length == 0
            ? _servers
            : _servers.Where(s => MatchesSearch(s, term));

        _filteredServers = query.OrderBy(s => s.Name).ToList();
        _selectedIds.IntersectWith(_filteredServers.Select(s => s.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();
        if (selectAll)
        {
            foreach (AuthLdapServer server in _filteredServers)
            {
                _selectedIds.Add(server.Id);
            }
        }
    }

    private void ToggleSelect(int serverId, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(serverId);
        }
        else
        {
            _selectedIds.Remove(serverId);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<AuthLdapServer> toDelete = await db.AuthLdapServers
            .Where(s => _selectedIds.Contains(s.Id))
            .ToListAsync();

        db.AuthLdapServers.RemoveRange(toDelete);
        await db.SaveChangesAsync();

        await LoadAsync();
    }

    private async Task CreateAsync()
    {
        if (!CanCreate)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();

        bool alreadyExists = await db.AuthLdapServers.AsNoTracking().AnyAsync(s => s.Name == _newServer.Name);
        if (alreadyExists)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Ce nom est déjà utilisé.")));
            return;
        }

        _newServer.BindPasswordProtected = Protector.Protect(_newBindPassword);
        db.AuthLdapServers.Add(_newServer);
        await db.SaveChangesAsync();

        _newServer = NewBlankServer();
        _newBindPassword = string.Empty;

        await JS.InvokeVoidAsync("glping.hideModal", "newLdapServerModal");
        await LoadAsync();
    }

    private static AuthLdapServer NewBlankServer() => new() { Name = string.Empty, Host = string.Empty, BaseDn = string.Empty };

    private static bool MatchesSearch(AuthLdapServer server, string term) =>
        server.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || server.Host.Contains(term, StringComparison.OrdinalIgnoreCase);
}
