namespace GlpiNg.Web.Components.Pages;

public partial class Config
{
    private sealed record ConfigSection(string Key, string Icon, string Label);

    private static readonly List<ConfigSection> Sections =
    [
        new("generale", "ti-adjustments-horizontal", "Configuration générale"),
        new("valeurs-par-defaut", "ti-adjustments", "Valeurs par défaut"),
        new("parc", "ti-box", "Parc"),
        new("assistance", "ti-headset", "Assistance"),
        new("gestion", "ti-wallet", "Gestion"),
        new("purge", "ti-trash", "Purge de l'historique"),
        new("systeme", "ti-settings", "Système"),
        new("securite", "ti-shield-lock", "Sécurité"),
        new("performance", "ti-gauge", "Performance"),
        new("api", "ti-plug", "API"),
        new("analyse-impact", "ti-affiliate", "Analyse d'impact"),
        new("colonnes", "ti-columns", "Colonnes par défaut"),
        new("network", "ti-network", "GLPI Network"),
        new("helpdesk", "ti-lifebuoy", "Helpdesk"),
        new("historique", "ti-history", "Historique"),
        new("tous", "ti-apps", "Tous"),
    ];

    private string _activeSection = "generale";

    private void SelectSection(string key)
    {
        _activeSection = key;
    }
}
