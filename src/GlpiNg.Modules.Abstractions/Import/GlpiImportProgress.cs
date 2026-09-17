namespace GlpiNg.Modules.Abstractions.Import;

/// <summary>
/// Avancement d'un import GLPI, tel qu'un service d'import le rapporte à l'écran qui l'a lancé.
/// </summary>
/// <param name="Phase">
/// Catégorie en cours, nommée par une des constantes de <see cref="GlpiImportPhases"/> : c'est ce
/// qui permet à l'appelant de la retrouver dans le plan qu'il a bâti depuis l'analyse.
/// </param>
/// <param name="Completed">Éléments traités dans cette phase depuis son début.</param>
/// <param name="Detail">
/// Ce sur quoi la phase travaille en ce moment, quand le compteur seul ne suffit pas à montrer
/// qu'elle avance : le téléchargement d'un fichier de paquet peut durer des minutes sans qu'aucun
/// élément ne soit terminé, et une barre immobile pendant ce temps se lit comme un blocage.
/// </param>
public sealed record GlpiImportProgress(string Phase, int Completed, string? Detail = null);

/// <summary>
/// Noms des phases d'import, partagés entre les services qui les rapportent et l'écran qui les
/// affiche.
///
/// Des constantes plutôt que des chaînes écrites des deux côtés : l'appelant apparie le rapport
/// d'avancement avec le volume que l'analyse avait annoncé pour cette même catégorie, et une
/// coquille dans un littéral ferait silencieusement stagner la barre au lieu de casser quoi que ce
/// soit. Ce sont aussi les libellés affichés — ils sont donc écrits pour être lus.
/// </summary>
public static class GlpiImportPhases
{
    // Parc (module Inventory).
    public const string Computers = "Ordinateurs";
    public const string Agents = "Agents";
    public const string Components = "Composants";
    public const string Monitors = "Moniteurs";
    public const string Softwares = "Logiciels";
    public const string Printers = "Imprimantes";
    public const string Peripherals = "Périphériques";
    public const string Volumes = "Volumes";
    public const string Batteries = "Batteries";

    // Administration (hôte).
    public const string Entities = "Entités";
    public const string Groups = "Groupes";
    public const string Profiles = "Profils";
    public const string Users = "Utilisateurs";
    public const string GeneralConfig = "Configuration générale";

    // Base de connaissances (hôte, pour le module du même nom).
    public const string KnowledgeBaseCategories = "Catégories de connaissances";
    public const string KnowledgeBaseArticles = "Articles de connaissances";
    public const string KnowledgeBaseTargets = "Visibilité des articles";
    public const string KnowledgeBaseRevisions = "Révisions des articles";
    public const string KnowledgeBaseDocuments = "Documents des articles";
    public const string KnowledgeBaseNotes = "Notes des articles";

    // Plugin d'inventaire (hôte).
    public const string IpRanges = "Plages IP";
    public const string SnmpCredentials = "Identifiants SNMP";
    public const string DeployPackages = "Paquets de déploiement";
    public const string UnmanagedDevices = "Actifs non gérés";
}
