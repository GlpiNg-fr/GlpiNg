namespace GlpiNg.Modules.Abstractions.Reports;

/// <summary>
/// Carte d'identité d'un rapport, telle que la page <c>/tools/reports</c> de l'hôte l'affiche dans
/// sa liste : c'est tout ce qu'il faut connaître d'un rapport pour le proposer, sans l'exécuter.
///
/// <paramref name="Key"/> est l'identifiant stable du rapport — il sert de dernier segment d'URL
/// (<c>/tools/reports/{key}</c>) et de clé de dispatch dans
/// <see cref="IReportProvider.RunAsync"/>, il doit donc rester utilisable tel quel dans une URL et
/// unique parmi tous les modules (d'où le préfixe de domaine : "parc-", "software-",
/// "deployment-"...).
///
/// <c>Category</c> est le regroupement à l'affichage ("Parc", "Logiciels", "Déploiement"...) :
/// libre, sans liste fermée — l'hôte se contente de grouper les rapports par cette chaîne, un
/// module apportant ses propres catégories comme il apporte ses propres groupes de menu.
/// <c>Icon</c> est un nom d'icône Tabler (ex. "ti-chart-bar"), comme <c>Menu.MenuItem.Icon</c>.
/// </summary>
public sealed record ReportDefinition(
    string Key,
    string Title,
    string Description,
    string Icon,
    string Category);
