namespace GlpiNg.Web.Models;

/// <summary>Onglet "Sécurité" : politique de mot de passe et 2FA.</summary>
public class SecuritySettings
{
    public bool EnforcePasswordPolicy { get; set; } = true;

    public int PasswordMinLength { get; set; } = 8;

    public bool PasswordNeedNumber { get; set; } = true;

    public bool PasswordNeedLowercase { get; set; } = true;

    public bool PasswordNeedUppercase { get; set; } = true;

    public bool PasswordNeedSymbol { get; set; }

    /// <summary>Jours avant expiration. -1 = jamais.</summary>
    public int PasswordExpirationDays { get; set; } = -1;

    /// <summary>Préavis d'expiration, en jours. -1 = notification désactivée.</summary>
    public int PasswordExpirationNoticeDays { get; set; } = -1;

    /// <summary>Délai avant désactivation du compte, en jours. -1 = ne pas désactiver.</summary>
    public int PasswordExpirationLockDelayDays { get; set; } = -1;

    /// <summary>Validité du jeton d'initialisation de mot de passe, en secondes.</summary>
    public int PasswordInitTokenDelay { get; set; } = 86400;

    public int NonReusablePasswordsCount { get; set; } = 1;

    public string TwoFactorSuffix { get; set; } = string.Empty;

    public bool TwoFactorEnforced { get; set; }
}
