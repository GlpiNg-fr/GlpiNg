namespace GlpiNg.Web.Models;

/// <summary>
/// Onglet "Administration &gt; Inventaire" (voir <c>Inventory/Index.razor</c>), calqué sur
/// /Inventory/Configuration de GLPI. GlpiNg n'analyse qu'un sous-ensemble du format
/// d'inventaire GLPI-Agent (voir le commentaire sur <c>InventoryContent</c>) : les réglages
/// ci-dessous marqués comme non branchés sont conservés pour la fidélité avec l'écran GLPI
/// mais ne pilotent aucun comportement réel faute de données correspondantes.
/// </summary>
public class InventorySettings
{
    /// <summary>Coupe la prise en compte des inventaires envoyés par les agents (voir AgentController.HandleInventoryAsync).</summary>
    public bool Enabled { get; set; } = true;

    public bool ImportVolumes { get; set; } = true;
    public bool ImportNetworkDrives { get; set; } = true;
    public bool ImportRemovableDrives { get; set; } = true;
    public bool ImportSoftwares { get; set; } = true;
    public bool ImportMonitors { get; set; }
    public bool ImportBatteries { get; set; } = true;

    public bool ImportCpus { get; set; } = true;
    public bool ImportMemories { get; set; } = true;
    public bool ImportDisks { get; set; } = true;
    public bool ImportNetworkCards { get; set; } = true;

    // --- Réglages présents dans GLPI mais non branchés dans GlpiNg (voir résumé en bas de page) ---
    public bool ImportPrinters { get; set; } = true;
    public bool ImportPeripherals { get; set; } = true;
    public bool ImportAntivirus { get; set; } = true;
    public bool ImportProcesses { get; set; } = true;
    public bool ImportEnvironments { get; set; }
    public bool ImportUnmanaged { get; set; } = true;

    public int InventoryFrequencyHours { get; set; } = 24;
    public string DefaultEntity { get; set; } = "Root entity";
    public bool ImportMonitorWithPartialSerialMatch { get; set; }

    public bool ImportVirtualMachines { get; set; }
    public bool CreateComputerForVirtualMachines { get; set; }
    public bool CreateComponentsForVirtualMachines { get; set; }

    public int AgentCleanupDays { get; set; } = 1;
}
