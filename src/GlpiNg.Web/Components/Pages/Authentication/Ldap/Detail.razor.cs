using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Authentication.Ldap;

public partial class Detail : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public int ServerId { get; set; }

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private AuthSecretProtector Protector { get; set; } = null!;

    [Inject]
    private LdapAuthenticationService LdapAuth { get; set; } = null!;

    [Inject]
    private NavigationManager Nav { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    private GlpiNgDbContext? _db;
    private AuthLdapServer? _server;
    private string _bindPasswordPlain = string.Empty;

    /// <summary>Vrai quand BindPasswordProtected est renseigné mais ne se déchiffre plus.</summary>
    private bool _storedPasswordUnreadable;
    private string _activeTabKey = "main";
    private bool _isSaving;
    private bool _isTesting;

    protected override async Task OnInitializedAsync()
    {
        _db = await DbFactory.CreateDbContextAsync();
        _server = await _db.AuthLdapServers.FirstOrDefaultAsync(s => s.Id == ServerId);

        // Déchiffrement à blanc : la seule façon de distinguer un secret utilisable d'un secret
        // devenu illisible, les deux étant présents en base.
        _storedPasswordUnreadable = _server?.BindPasswordProtected is not null
                                    && Protector.Unprotect(_server.BindPasswordProtected) is null;
    }

    private void SetTab(string key) => _activeTabKey = key;

    private async Task SaveAsync()
    {
        if (_db is null || _server is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            if (!string.IsNullOrEmpty(_bindPasswordPlain))
            {
                _server.BindPasswordProtected = Protector.Protect(_bindPasswordPlain);
                _bindPasswordPlain = string.Empty;
            }

            _server.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, "Annuaire enregistré."));
        }
        finally
        {
            _isSaving = false;
        }
    }

    // Teste avec le mot de passe en cours de saisie (s'il y en a un) plutôt qu'avec la valeur
    // déjà enregistrée, sans l'écrire en base : évite d'imposer "Sauvegarder" avant de pouvoir
    // vérifier une modification du compte de connexion.
    private async Task TestConnectionAsync()
    {
        if (_server is null)
        {
            return;
        }

        AuthLdapServer probe = new()
        {
            Name = _server.Name,
            Host = _server.Host,
            Port = _server.Port,
            UseSsl = _server.UseSsl,
            BaseDn = _server.BaseDn,
            UseBind = _server.UseBind,
            BindDn = _server.BindDn,
            BindPasswordProtected = string.IsNullOrEmpty(_bindPasswordPlain)
                ? _server.BindPasswordProtected
                : Protector.Protect(_bindPasswordPlain),
            LoginField = _server.LoginField,
        };

        _isTesting = true;

        try
        {
            LdapConnectionTestResult testResult = await Task.Run(() => LdapAuth.TestServerConnection(probe));
            ToastService.Notify(new ToastMessage(testResult.Success ? ToastType.Success : ToastType.Danger, testResult.Message));
        }
        finally
        {
            _isTesting = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (_db is null || _server is null)
        {
            return;
        }

        _db.AuthLdapServers.Remove(_server);
        await _db.SaveChangesAsync();

        Nav.NavigateTo("/config/auth/ldap");
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
