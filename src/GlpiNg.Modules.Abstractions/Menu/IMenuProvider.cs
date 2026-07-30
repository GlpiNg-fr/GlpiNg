namespace GlpiNg.Modules.Abstractions.Menu;

/// <summary>
/// Implémenté par un module pour contribuer des groupes/entrées au menu latéral de
/// l'hôte (GlpiNg.Web), plutôt que de faire coder ces entrées en dur dans MainLayout.
/// Chaque module s'enregistre en DI (ex. <c>services.AddSingleton&lt;IMenuProvider, ...&gt;()</c>
/// dans son extension AddXxxModule) ; l'hôte résout <see cref="IEnumerable{T}"/> de
/// IMenuProvider et fusionne les groupes obtenus avec les siens.
/// </summary>
public interface IMenuProvider
{
    IReadOnlyList<MenuGroup> GetMenuGroups();
}
