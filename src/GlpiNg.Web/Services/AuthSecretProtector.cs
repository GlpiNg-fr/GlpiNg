using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace GlpiNg.Web.Services;

/// <summary>
/// Chiffre au repos les secrets d'authentification (mot de passe du compte de connexion
/// d'un <see cref="Models.AuthLdapServer"/>) via l'API DataProtection déjà utilisée
/// implicitement par l'application (anti-forgery, cookie d'authentification) — pas de clé
/// à gérer séparément, la clé applicative sert de racine.
/// </summary>
public class AuthSecretProtector
{
    private readonly IDataProtector _protector;

    public AuthSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("GlpiNg.AuthSecretProtector.v1");
    }

    public string? Protect(string? plainText) =>
        string.IsNullOrEmpty(plainText) ? null : _protector.Protect(plainText);

    /// <summary>Renvoie null si la valeur est absente ou n'a pas pu être déchiffrée (clé applicative changée, valeur corrompue).</summary>
    public string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(protectedText);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
