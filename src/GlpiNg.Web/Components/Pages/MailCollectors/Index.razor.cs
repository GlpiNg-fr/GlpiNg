using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.MailCollectors;

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

    private List<MailCollector> _collectors = [];
    private List<MailCollector> _filtered = [];
    private List<GlpiEntity> _entities = [];
    private string _searchTerm = string.Empty;
    private readonly HashSet<int> _selectedIds = [];
    private MailCollector _new = NewBlank();
    private string? _newPassword;

    private bool AllSelected => _filtered.Count > 0 && _selectedIds.Count == _filtered.Count;

    private bool CanCreate =>
        !string.IsNullOrWhiteSpace(_new.Name)
        && !string.IsNullOrWhiteSpace(_new.Host)
        && !string.IsNullOrWhiteSpace(_new.Login);

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _collectors = await db.MailCollectors.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        _entities = await db.Entities.AsNoTracking().OrderBy(e => e.Name).ToListAsync();

        _selectedIds.Clear();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string term = _searchTerm.Trim();

        _filtered = (term.Length == 0
                ? _collectors
                : _collectors.Where(c => Matches(c, term)))
            .OrderBy(c => c.Name)
            .ToList();

        _selectedIds.IntersectWith(_filtered.Select(c => c.Id));
    }

    private void ToggleSelectAll(bool selectAll)
    {
        _selectedIds.Clear();

        if (selectAll)
        {
            foreach (MailCollector collector in _filtered)
            {
                _selectedIds.Add(collector.Id);
            }
        }
    }

    private void ToggleSelect(int id, bool selected)
    {
        if (selected)
        {
            _selectedIds.Add(id);
        }
        else
        {
            _selectedIds.Remove(id);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedIds.Count == 0)
        {
            return;
        }

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        List<MailCollector> toDelete = await db.MailCollectors.Where(c => _selectedIds.Contains(c.Id)).ToListAsync();

        db.MailCollectors.RemoveRange(toDelete);
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

        if (await db.MailCollectors.AsNoTracking().AnyAsync(c => c.Name == _new.Name))
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Ce nom est déjà utilisé.")));
            return;
        }

        _new.PasswordProtected = Protector.Protect(_newPassword);

        db.MailCollectors.Add(_new);
        await db.SaveChangesAsync();

        _new = NewBlank();
        _newPassword = null;

        await JS.InvokeVoidAsync("glping.hideModal", "newMailCollectorModal");
        await LoadAsync();
    }

    private static MailCollector NewBlank() => new()
    {
        Name = string.Empty,
        Host = string.Empty,
        Login = string.Empty,
    };

    private static bool Matches(MailCollector collector, string term) =>
        collector.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
        || collector.Host.Contains(term, StringComparison.OrdinalIgnoreCase)
        || collector.Login.Contains(term, StringComparison.OrdinalIgnoreCase);
}
