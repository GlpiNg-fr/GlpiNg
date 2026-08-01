namespace GlpiNg.Web.Models;

/// <summary>
/// État d'activation des modules pas encore construits (entrées de menu affichées en
/// "pas encore disponible" dans <see cref="Components.Layout.MainLayout"/> et
/// <c>InventoryMenuProvider</c>). La clé est <c>"{groupe}:{libellé}"</c>, ce qui évite
/// de faire porter une clé stable aux enregistrements <c>MenuItem</c>/<c>NavItem</c>
/// existants. Un module absent du dictionnaire est considéré actif.
/// </summary>
public class ModulesSettings
{
    public Dictionary<string, bool> Enabled { get; set; } = [];

    public bool IsEnabled(string groupKey, string label) =>
        !Enabled.TryGetValue($"{groupKey}:{label}", out var value) || value;
}
