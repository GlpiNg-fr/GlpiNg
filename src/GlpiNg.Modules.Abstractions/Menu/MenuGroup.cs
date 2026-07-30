namespace GlpiNg.Modules.Abstractions.Menu;

/// <summary>
/// Contribution d'un module à un groupe du menu latéral, identifié par <paramref name="Key"/>.
/// Si l'hôte (ou un autre module) contribue déjà un groupe avec la même clé, les
/// <paramref name="Items"/> sont ajoutés à la suite de ce groupe plutôt que de créer un
/// doublon — voir la fusion dans MainLayout. <paramref name="Icon"/>/<paramref name="Label"/>
/// ne sont utilisés que si le groupe n'existe pas encore.
/// </summary>
public sealed record MenuGroup(string Key, string Icon, string Label, IReadOnlyList<MenuItem> Items);
