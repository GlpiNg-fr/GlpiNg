using System.Security.Claims;
using BlazorBootstrap;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Components.Pages.MyAccount;

/// <summary>
/// Auto-édition du compte de l'utilisateur connecté ("Mon profil", menu utilisateur de la
/// topbar — voir MainLayout.razor). Contrairement à Users/Detail (fiche Administration, tous
/// utilisateurs), pas d'onglets Habilitations/Groupes/Sécurité ni de champs Actif/Administrateur :
/// un utilisateur ne peut ni changer ses propres droits, ni celui de son compte. L'identifiant
/// reste aussi en lecture seule (c'est l'identité de connexion). Un changement de mot de passe
/// exige ici le mot de passe actuel (contrairement à la réinitialisation admin sur la fiche
/// Utilisateurs), et n'est proposé que pour un compte AuthSource.Local (voir GlpiUser.AuthSource).
/// </summary>
public partial class Profile : ComponentBase, IAsyncDisposable
{
    private static readonly PasswordHasher<GlpiUser> Hasher = new();

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [CascadingParameter]
    private Task<AuthenticationState>? AuthStateTask { get; set; }

    private GlpiNgDbContext? _db;
    private GlpiUser? _user;
    private int? _userId;
    private bool _isSaving;
    private bool _showPasswordFields;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _newPasswordConfirm = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        if (AuthStateTask is not null)
        {
            var authState = await AuthStateTask;
            string? userIdClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _userId = int.TryParse(userIdClaim, out int userId) ? userId : null;
        }

        if (_userId is not int uid)
        {
            return;
        }

        _db = await DbFactory.CreateDbContextAsync();
        _user = await _db.Users.Include(u => u.LdapServer).FirstOrDefaultAsync(u => u.Id == uid);
    }

    private void TogglePasswordFields()
    {
        _showPasswordFields = !_showPasswordFields;
        _currentPassword = string.Empty;
        _newPassword = string.Empty;
        _newPasswordConfirm = string.Empty;
    }

    private async Task SaveAsync()
    {
        if (_db is null || _user is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            if (_showPasswordFields && (!string.IsNullOrEmpty(_currentPassword) || !string.IsNullOrEmpty(_newPassword) || !string.IsNullOrEmpty(_newPasswordConfirm)))
            {
                if (Hasher.VerifyHashedPassword(_user, _user.PasswordHash, _currentPassword) == PasswordVerificationResult.Failed)
                {
                    ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Le mot de passe actuel est incorrect.")));
                    return;
                }

                if (string.IsNullOrEmpty(_newPassword) || _newPassword != _newPasswordConfirm)
                {
                    ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Les nouveaux mots de passe ne correspondent pas.")));
                    return;
                }

                _user.PasswordHash = Hasher.HashPassword(_user, _newPassword);
                TogglePasswordFields();

                _db.UserHistoryEntries.Add(new GlpiUserHistoryEntry
                {
                    UserId = _user.Id,
                    User = _user.DisplayName ?? _user.UserName,
                    Field = "Mot de passe",
                    Description = "Mot de passe modifié"
                });
            }

            _user.DisplayName = ComputeDisplayName(_user);

            await _db.SaveChangesAsync();
            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Profil enregistré.")));
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>GLPI calcule le nom complet à partir de Nom/Prénom plutôt que de le laisser saisir séparément.</summary>
    private static string ComputeDisplayName(GlpiUser user)
    {
        string fullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return fullName.Length > 0 ? fullName : user.UserName;
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}
