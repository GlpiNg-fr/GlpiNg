namespace GlpiNg.Web.Models;

/// <summary>
/// Modèle de l'onglet "Configuration générale" de la page /config. Réglages "GeneralSettings",
/// lus et écrits par <see cref="Services.SettingsCacheService"/> (table AppSettings + cache
/// mémoire) — pris en compte immédiatement, sans redémarrage.
/// </summary>
public class GeneralSettings
{
    public string ApplicationUrl { get; set; } = "http://localhost";

    public string? LoginPageMessage { get; set; }

    public string? SimplifiedInterfaceHelpLink { get; set; }

    public string? StandardInterfaceHelpLink { get; set; }

    public int DefaultDecimalsCount { get; set; } = 2;

    public bool AllowAnonymousFaqAccess { get; set; }

    public bool AllowAnonymousFileImports { get; set; }

    public int DefaultDropdownListLimit { get; set; } = 100;

    public int SearchEngineDisableThreshold { get; set; } = 50;

    /// <summary>Éléments visualisés (allow_search_view) : 0 = Non, 1 = Oui (dernier critère), 2 = Oui (critère par défaut).</summary>
    public int SearchViewMode { get; set; } = 2;

    public bool AllowGlobalSearch { get; set; } = true;

    /// <summary>Tous (allow_search_all) : 0 = Non, 1 = Oui (dernier critère).</summary>
    public int SearchAllListMode { get; set; } = 1;

    public int MaxSearchResultsPerPage { get; set; } = 50;

    public int SearchResultsSummaryLength { get; set; } = 500;

    public int SearchResultsMaxUrlLength { get; set; } = 30;

    /// <summary>Temps de rétention "se souvenir de moi" (login_remember_time), en secondes. 0 = désactivé.</summary>
    public int RememberMeDuration { get; set; } = 5184000;

    public bool RememberMeDefaultChecked { get; set; } = true;

    public bool ShowAuthSourcesOnLoginPage { get; set; }

    /// <summary>Format d'affichage des adresses MAC : "Windows" (AA-BB-CC-DD-EE-FF), "HP" (AABBCC-DDEEFF), "Cisco" (aabb.ccdd.eeff).</summary>
    public string MacAddressFormat { get; set; } = "Windows";
}
