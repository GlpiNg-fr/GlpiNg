using GlpiNg.Modules.Abstractions.Localization;
namespace GlpiNg.Web.Models;

/// <summary>
/// Onglet "Purge de l'historique" : délai de conservation par type d'entrée, en mois.
/// -1 = supprimer tout de suite, 0 = conserver indéfiniment, N = supprimer au-delà de N mois.
/// </summary>
public class PurgeSettings
{
    public Dictionary<string, int> RetentionMonths { get; set; } = BuildDefaults();

    public static readonly (string Key, string Label)[] Fields =
    [
        ("AddRelation", "Ajout de relation entre objets"),
        ("DeleteRelation", "Suppression de relations entre objets"),
        ("CreateItem", "Ajouter l'élément"),
        ("DeleteItem", "Supprimer l'élément"),
        ("RestoreItem", "Restaurer l'élément"),
        ("UpdateItem", "Mettre à jour l'élément"),
        ("Comments", "Commentaires"),
        ("DateMod", "Dernière modification"),
        ("Plugins", "Plugins"),
        ("RefusedEquipment", "Historique des équipements refusés"),
        ("ItemSoftwareInstall", "Installation / désinstallation des logiciels sur les éléments"),
        ("SoftwareVersionInstall", "Versions d'installation/désinstallation sur les logiciels"),
        ("SoftwareItemInstall", "Ajout / Suppression des versions logiciels sur les éléments"),
        ("InfocomCreation", "Ajout des informations financières à un objet"),
        ("ProfileUser", "Ajout / Suppression de profils aux utilisateurs"),
        ("GroupUser", "Ajout / Suppression de groupes aux utilisateurs"),
        ("UserAuthChanges", "Changement de type d'authentification sur les utilisateurs"),
        ("UserDeletedFromLdap", "Utilisateur supprimé de l'annuaire LDAP"),
        ("AddDevice", "Ajouter un composant"),
        ("UpdateDevice", "Mettre à jour un composant"),
        ("DisconnectDevice", "Déconnecter un composant"),
        ("ConnectDevice", "Connecter un composant"),
        ("DeleteDevice", "Supprimer un composant"),
        ("All", "Purger toutes les entrées de l'historique"),
    ];

    public static Dictionary<string, int> BuildDefaults()
    {
        var defaults = new Dictionary<string, int>();
        foreach (var (key, _) in Fields)
        {
            defaults[key] = key == "All" ? 0 : 3;
        }

        return defaults;
    }

    /// <summary>Options -1..120 mois communes à tous les champs de cet onglet.</summary>
    public static IEnumerable<(int Value, string Label)> RetentionOptions()
    {
        yield return (-1, "Supprimer tout");
        yield return (0, "Conserver tout");
        for (var months = 1; months <= 120; months++)
        {
            yield return (months, Tr.T("Supprimer si plus ancien que {0} mois", months));
        }
    }
}
