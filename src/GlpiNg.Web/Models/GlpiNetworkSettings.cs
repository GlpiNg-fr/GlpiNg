namespace GlpiNg.Web.Models;

/// <summary>Onglet "GLPI Network".</summary>
public class GlpiNetworkSettings
{
    public string RegistrationKey { get; set; } = string.Empty;

    /// <summary>ask, replace ou never.</summary>
    public string MarketplaceBehavior { get; set; } = "ask";
}
