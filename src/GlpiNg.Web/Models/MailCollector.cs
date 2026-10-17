namespace GlpiNg.Web.Models;

/// <summary>
/// Collecteur de courriels (glpi_mailcollectors côté GLPI) : une boîte IMAP relevée
/// périodiquement par <c>Services.Mail.MailCollectorCronTask</c>, dont chaque message devient un
/// ticket.
///
/// Distinct de <see cref="AuthMailServer"/>, qui décrit un serveur mail servant à
/// <i>authentifier</i> des comptes : ici le serveur sert à <i>lire</i> une boîte, avec un compte
/// à lui et un dossier à relever.
///
/// IMAP seulement — POP n'a ni dossiers ni drapeau « lu », les deux mécanismes sur lesquels
/// repose la relève (voir <c>MailCollectorService</c>).
/// </summary>
public class MailCollector
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Comment { get; set; }

    public required string Host { get; set; }

    /// <summary>Port du serveur ; laissé vide, MailKit prend celui du chiffrement choisi.</summary>
    public int? Port { get; set; }

    public MailServerEncryption Encryption { get; set; } = MailServerEncryption.Ssl;
    public bool ValidateCertificate { get; set; } = true;

    public required string Login { get; set; }

    /// <summary>Mot de passe du compte relevé, chiffré au repos via Services.AuthSecretProtector.</summary>
    public string? PasswordProtected { get; set; }

    /// <summary>Dossier relevé. « INBOX » sauf boîte partagée rangée par règles côté serveur.</summary>
    public string Folder { get; set; } = "INBOX";

    /// <summary>
    /// Dossier où ranger un message une fois son ticket ouvert. Vide, le message reste en place et
    /// n'est que marqué lu — ce qui suffit à ne pas le reprendre au passage suivant.
    /// </summary>
    public string? MoveToFolder { get; set; }

    /// <summary>Entité des tickets ouverts par ce collecteur. Vide = l'entité racine.</summary>
    public int? EntityId { get; set; }
    public GlpiEntity? Entity { get; set; }

    /// <summary>
    /// Nombre de messages traités par passage. Borne volontaire : une boîte laissée pleine pendant
    /// un mois ne doit pas ouvrir mille tickets d'un coup ni tenir la tâche cron une heure.
    /// </summary>
    public int MaxFetchPerRun { get; set; } = 20;

    /// <summary>Joindre les pièces attachées au ticket créé, en documents.</summary>
    public bool ImportAttachments { get; set; } = true;

    // --- Compte rendu de la dernière relève, affiché sur la fiche : sans lui, un collecteur en
    // échec (mot de passe changé, dossier renommé) reste muet jusqu'à ce qu'on lise les journaux.
    public DateTime? LastRunAt { get; set; }
    public int LastImportedCount { get; set; }
    public string? LastError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
