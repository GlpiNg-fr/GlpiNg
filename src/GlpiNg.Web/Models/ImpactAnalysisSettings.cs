namespace GlpiNg.Web.Models;

/// <summary>Onglet "Analyse d'impact" : types d'éléments inclus dans les graphes d'impact.</summary>
public class ImpactAnalysisSettings
{
    public List<string> EnabledItemTypes { get; set; } =
    [
        "Computer", "Monitor", "NetworkEquipment", "Peripheral", "Printer",
        "Rack", "Enclosure", "PDU", "Datacenter", "Cluster", "Software",
        "DatabaseInstance", "Domain", "Appliance"
    ];

    public static readonly (string Key, string Label)[] AvailableItemTypes =
    [
        ("Computer", "Ordinateur"),
        ("Monitor", "Moniteur"),
        ("NetworkEquipment", "Matériel réseau"),
        ("Peripheral", "Périphérique"),
        ("Printer", "Imprimante"),
        ("Rack", "Baie"),
        ("Enclosure", "Châssis"),
        ("PDU", "PDU"),
        ("Datacenter", "Data center"),
        ("DCRoom", "Salle serveur"),
        ("Cluster", "Cluster"),
        ("Software", "Logiciel"),
        ("DatabaseInstance", "Instance de base de données"),
        ("Domain", "Domaine"),
        ("Appliance", "Applicatif"),
        ("Contract", "Contrat"),
    ];
}
