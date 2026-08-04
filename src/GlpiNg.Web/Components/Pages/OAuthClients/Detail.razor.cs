using System.Security.Cryptography;
using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;

namespace GlpiNg.Web.Components.Pages.OAuthClients;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    private static readonly PasswordHasher<OAuthClient> SecretHasher = new();

    [Parameter]
    public int ClientId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private GlpiNgDbContext? _db;
    private OAuthClient? _client;
    private string? _revealSecret;
    private bool _isSaving;
    private bool _isRegenerating;

    protected override async Task OnInitializedAsync()
    {
        _db = await DbFactory.CreateDbContextAsync();
        _client = await _db.OAuthClients.FirstOrDefaultAsync(c => c.Id == ClientId);
    }

    private async Task SaveAsync()
    {
        if (_db is null || _client is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            _client.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Client enregistré."));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task RegenerateSecretAsync()
    {
        if (_db is null || _client is null)
        {
            return;
        }

        _isRegenerating = true;

        try
        {
            string plainSecret = RandomNumberGenerator.GetHexString(48, lowercase: true);
            _client.ClientSecretHash = SecretHasher.HashPassword(_client, plainSecret);
            _client.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _revealSecret = plainSecret;
            ToastService.Notify(new ToastMessage(ToastType.Success, "Secret régénéré."));
        }
        finally
        {
            _isRegenerating = false;
        }
    }

    private async Task CopyClientIdAsync()
    {
        if (_client is not null)
        {
            await JS.InvokeVoidAsync("glpiNg.copyToClipboard", _client.ClientId);
        }
    }

    private async Task CopySecretAsync()
    {
        if (_revealSecret is not null)
        {
            await JS.InvokeVoidAsync("glpiNg.copyToClipboard", _revealSecret);
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _client is null)
        {
            return;
        }

        _db.OAuthClients.Remove(_client);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/config/oauth-clients");
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
