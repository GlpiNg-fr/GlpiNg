namespace GlpiNg.Modules.Abstractions.Menu;

/// <summary>
/// Une entrée du menu latéral. <paramref name="Href"/> à <c>null</c> indique une entrée
/// pas encore disponible (affichée désactivée par l'hôte). <paramref name="Icon"/> est un nom
/// d'icône Tabler (ex. "ti-device-desktop") ; à défaut, l'hôte affiche une puce neutre.
/// </summary>
public sealed record MenuItem(string Label, string? Href = null, string? Icon = null);
