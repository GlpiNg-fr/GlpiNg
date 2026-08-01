namespace GlpiNg.Web.Models;

/// <summary>
/// Ordre des cartes du tableau de bord "Parc", mémorisé par utilisateur pour être
/// restauré à la reconnexion. <see cref="CardOrder"/> est la liste des clés de carte
/// séparées par des virgules, dans l'ordre d'affichage souhaité.
/// </summary>
public class DashboardCardPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string CardOrder { get; set; } = string.Empty;
}
