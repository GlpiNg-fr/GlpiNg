namespace GlpiNg.Web.Models;

/// <summary>Onglet "Parc" : réglages généraux applicables au parc matériel/logiciel.</summary>
public class ParcSettings
{
    public bool AutoCreateFinancialInfo { get; set; }

    /// <summary>0 = Oui (unitaire), 1 = Oui (globale), 2 = Non.</summary>
    public int MonitorsManagementRestrict { get; set; } = 2;

    /// <summary>0 = Oui (unitaire), 1 = Oui (globale), 2 = Non.</summary>
    public int PeripheralsManagementRestrict { get; set; } = 2;

    /// <summary>0 = Oui (unitaire), 1 = Oui (globale), 2 = Non.</summary>
    public int PhonesManagementRestrict { get; set; } = 2;

    /// <summary>0 = Oui (unitaire), 1 = Oui (globale), 2 = Non.</summary>
    public int PrintersManagementRestrict { get; set; } = 2;

    /// <summary>0 = Global, 1 = Par entité.</summary>
    public int AutonameScope { get; set; } = 1;
}
