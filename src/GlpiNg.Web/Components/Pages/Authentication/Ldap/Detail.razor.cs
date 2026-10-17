using GlpiNg.Modules.Abstractions.Localization;
﻿using BlazorBootstrap;
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
    private string _filterTestLogin = string.Empty;

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

        // Un filtre mal formé ferait échouer toutes les connexions de l'annuaire : refusé ici, avec
        // la raison, plutôt que découvert à la prochaine connexion dans le journal du serveur.
        (string? loginFilter, string? filterError) = LdapFilter.Normalize(_server.LoginFilter);
        if (filterError is not null)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Filtre de connexion invalide : {0}", filterError)));
            return;
        }

        _server.LoginFilter = loginFilter;
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
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Annuaire enregistré.")));
        }
        finally
        {
            _isSaving = false;
        }
    }

    // Teste avec le mot de passe en cours de saisie (s'il y en a un) plutôt qu'avec la valeur
    // déjà enregistrée, sans l'écrire en base : évite d'imposer "Sauvegarder" avant de pouvoir
    // vérifier une modification du compte de connexion.
    private Task TestConnectionAsync() => RunTestAsync(probe => LdapAuth.TestServerConnection(probe));

    /// <summary>
    /// Ce compte peut-il se connecter ? Avec le filtre tel qu'il est saisi, enregistré ou non : c'est
    /// en le mettant au point qu'on en a besoin.
    /// </summary>
    private Task TestFilterAsync() => RunTestAsync(probe => LdapAuth.CheckLoginFilter(probe, _filterTestLogin));

    private async Task RunTestAsync(Func<AuthLdapServer, LdapConnectionTestResult> test)
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
            UseStartTls = _server.UseStartTls,
            TimeoutSeconds = _server.TimeoutSeconds,
            BaseDn = _server.BaseDn,
            UseBind = _server.UseBind,
            BindDn = _server.BindDn,
            BindPasswordProtected = string.IsNullOrEmpty(_bindPasswordPlain)
                ? _server.BindPasswordProtected
                : Protector.Protect(_bindPasswordPlain),
            LoginField = _server.LoginField,
            LoginFilter = _server.LoginFilter,
        };

        _isTesting = true;

        try
        {
            LdapConnectionTestResult testResult = await Task.Run(() => test(probe));
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
