namespace GlpiNg.Modules.Abstractions.Menu;

/// <summary>
/// Une entrée du menu latéral. <paramref name="Href"/> à <c>null</c> indique une entrée
/// pas encore disponible (affichée désactivée par l'hôte).
/// </summary>
public sealed record MenuItem(string Label, string? Href = null);
