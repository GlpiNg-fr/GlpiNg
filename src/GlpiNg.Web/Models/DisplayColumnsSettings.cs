namespace GlpiNg.Web.Models;

/// <summary>Onglet "Colonnes par défaut" : colonnes affichées par défaut dans les listes, par type d'objet.</summary>
public class DisplayColumnsSettings
{
    public Dictionary<string, List<string>> ColumnsByItemType { get; set; } = BuildDefaults();

    public static readonly (string ItemType, string Label)[] ItemTypes =
    [
        ("Computer", "Ordinateurs"),
        ("Monitor", "Moniteurs"),
        ("Software", "Logiciels"),
        ("NetworkEquipment", "Matériels réseau"),
        ("Peripheral", "Périphériques"),
        ("Printer", "Imprimantes"),
        ("Phone", "Téléphones"),
        ("Ticket", "Tickets"),
        ("Contract", "Contrats"),
        ("Budget", "Budgets"),
    ];

    public static readonly (string Key, string Label)[] AvailableColumns =
    [
        ("name", "Nom"),
        ("id", "ID"),
        ("status", "État"),
        ("itemtype", "Type"),
        ("model", "Modèle"),
        ("manufacturer", "Fabricant"),
        ("location", "Emplacement"),
        ("serial", "Numéro de série"),
        ("entity", "Entité"),
        ("user", "Utilisateur"),
        ("group", "Groupe"),
        ("date_creation", "Date de création"),
        ("date_mod", "Dernière mise à jour"),
        ("comment", "Commentaire"),
    ];

    private static Dictionary<string, List<string>> BuildDefaults() =>
        ItemTypes.ToDictionary(t => t.ItemType, _ => new List<string> { "name", "status", "location", "date_mod" });
}
