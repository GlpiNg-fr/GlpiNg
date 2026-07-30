namespace GlpiNg.Web.Models;

/// <summary>
/// Modèle de l'onglet "Configuration générale" de la page /config. Reflète la
/// section "GeneralSettings" de appsettings.json, lue et écrite par
/// <see cref="Services.AppSettingsFileStore"/>. Ces réglages sont pris en compte
/// sans redémarrage (appsettings.json est rechargé automatiquement).
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
}
