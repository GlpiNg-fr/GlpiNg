using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Prépare un compte issu d'un annuaire : report des attributs sur les champs de la fiche, et
/// habilitation de départ.
///
/// Extrait de <see cref="UserCredentialAuthenticator"/> pour être partagé avec l'import manuel
/// (<see cref="LdapUserImportService"/>) : un compte importé depuis Administration &gt;
/// Utilisateurs et un compte provisionné à la première connexion doivent être indiscernables,
/// ce qui ne tient que si un seul code les prépare.
/// </summary>
public sealed class LdapAccountProvisioner(GlpiNgDbContext db, EntityTreeCache entityTree)
{
    /// <summary>
    /// Reporte les attributs de l'annuaire sur le compte, selon la correspondance configurée dans
    /// l'onglet « Utilisateurs » de la fiche annuaire. Un attribut non configuré ou absent laisse
    /// le champ inchangé : l'annuaire complète le compte, il ne l'efface pas.
    /// </summary>
    public void ApplyAttributes(GlpiUser user, AuthLdapServer server, IReadOnlyDictionary<string, string> attributes)
    {
        string? Value(string? fieldName)
            => string.IsNullOrWhiteSpace(fieldName) ? null : attributes.GetValueOrDefault(fieldName.Trim());

        user.LastName = Value(server.LastNameField) ?? user.LastName;
        user.FirstName = Value(server.FirstNameField) ?? user.FirstName;
        user.Email = Value(server.EmailField) ?? user.Email;
        user.Phone = Value(server.PhoneField) ?? Value(server.MobileField) ?? user.Phone;
        user.Location = Value(server.LocationField) ?? user.Location;

        // Nom affiché : l'attribut dédié s'il est renseigné, sinon reconstruit depuis prénom et
        // nom — plutôt que de laisser l'identifiant, qui est ce que la création met par défaut.
        string? displayName = Value(server.DisplayNameField);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            string composed = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(part => !string.IsNullOrWhiteSpace(part)));
            displayName = composed.Length > 0 ? composed : null;
        }

        user.DisplayName = displayName ?? user.DisplayName;
    }

    /// <summary>
    /// Donne au compte une habilitation de départ, sur l'entité dont le TAG d'affectation
    /// correspond à celui de l'annuaire (onglet « Informations avancées »), et sur l'entité racine
    /// à défaut — même mécanisme de TAG que pour les postes inventoriés.
    ///
    /// Le profil retenu est celui marqué par défaut. Sans profil par défaut, aucune habilitation
    /// n'est créée : le compte existe mais ne voit rien, ce qui est le comportement voulu pour un
    /// utilisateur sans droits et se corrige depuis sa fiche.
    ///
    /// Sans effet si le compte a déjà une habilitation : un import relancé ne doit pas en empiler.
    /// </summary>
    public async Task AssignDefaultHabilitationAsync(GlpiUser user, AuthLdapServer server, CancellationToken ct = default)
    {
        if (await db.UserProfiles.AnyAsync(habilitation => habilitation.UserId == user.Id, ct))
        {
            return;
        }

        int? entityId = entityTree.GetEntityIdByAssignmentTag(server.EntityAssignmentTag)
                        ?? entityTree.GetRootEntityId();

        if (entityId is not int targetEntityId)
        {
            return;
        }

        GlpiProfile? defaultProfile = await db.Profiles.FirstOrDefaultAsync(profile => profile.IsDefault, ct);
        if (defaultProfile is null)
        {
            return;
        }

        db.UserProfiles.Add(new GlpiUserProfile
        {
            UserId = user.Id,
            EntityId = targetEntityId,
            ProfileId = defaultProfile.Id,
            IsRecursive = false,
        });
    }
}
