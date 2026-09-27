using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Components.Pages.Authentication.Mail;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int ServerId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private GlpiNgDbContext? _db;
    private AuthMailServer? _server;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        _db = await DbFactory.CreateDbContextAsync();
        _server = await _db.AuthMailServers.FirstOrDefaultAsync(s => s.Id == ServerId);
    }

    private async Task SaveAsync()
    {
        if (_db is null || _server is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            _server.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Serveur enregistré.")));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _server is null)
        {
            return;
        }

        _db.AuthMailServers.Remove(_server);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/config/auth/mail");
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
