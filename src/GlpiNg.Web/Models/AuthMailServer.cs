namespace GlpiNg.Web.Models;

public enum MailServerProtocol
{
    Imap = 0,
    Pop = 1,
}

public enum MailServerEncryption
{
    None = 0,
    Ssl = 1,
    Tls = 2,
}

/// <summary>
/// Serveur mail externe (glpi_authmails côté GLPI), reprenant les champs du formulaire
/// "Serveur mails" de GLPI (front/authmail.form.php). Gestion/CRUD uniquement pour
/// l'instant : contrairement à <see cref="AuthLdapServer"/>, il n'est pas branché sur le
/// flux de connexion (AccountController) — GlpiNg n'authentifie aujourd'hui que par
/// mot de passe local ou bind LDAP.
/// </summary>
public class AuthMailServer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Comment { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public required string Host { get; set; }
    public int? Port { get; set; }
    public MailServerProtocol Protocol { get; set; } = MailServerProtocol.Imap;
    public MailServerEncryption Encryption { get; set; } = MailServerEncryption.None;
    public bool ValidateCertificate { get; set; } = true;

    public string? EmailDomain { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
