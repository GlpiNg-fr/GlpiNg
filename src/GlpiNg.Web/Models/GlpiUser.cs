namespace GlpiNg.Web.Models;

/// <summary>
/// Compte utilisateur GlpiNg. Modèle minimal pour l'instant — juste ce qu'il
/// faut pour que le compte administrateur créé par AnthoDingo.Setup existe
/// quelque part. L'authentification applicative (login, sessions) n'est pas
/// encore implémentée : c'est un chantier séparé.
/// </summary>
public class GlpiUser
{
    public int Id { get; set; }
    public required string UserName { get; set; }
    public string? DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsAdmin { get; set; }
    public bool SidebarCollapsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
