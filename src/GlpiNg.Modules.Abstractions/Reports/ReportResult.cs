using System.Globalization;

namespace GlpiNg.Modules.Abstractions.Reports;

/// <summary>
/// Résultat d'un rapport : un ou plusieurs tableaux, déjà calculés et déjà formatés.
///
/// Volontairement du texte et non des objets typés : le module sait ce que veulent dire ses
/// chiffres (une date d'inventaire, une part du parc, un nombre d'installations), l'hôte ne sait
/// qu'afficher et exporter. C'est ce qui permet à la page <c>/tools/reports/{key}</c> et aux
/// exports CSV/XLSX/ODS/PDF d'être écrits une seule fois pour tous les rapports, présents et à
/// venir, sans que l'hôte connaisse un seul modèle de données de module.
/// </summary>
public sealed record ReportResult(IReadOnlyList<ReportTable> Tables)
{
    public static ReportResult Of(params ReportTable[] tables) => new(tables);
}

/// <summary>
/// Un tableau du rapport. <paramref name="Title"/> est affiché au-dessus et sert de nom d'onglet
/// dans les exports tableur ; <paramref name="EmptyMessage"/> remplace le tableau quand il n'y a
/// aucune ligne — un rapport vide doit dire pourquoi il l'est, pas se contenter de disparaître.
/// </summary>
public sealed record ReportTable(
    string Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    string? EmptyMessage = null);

/// <summary>Nature d'une colonne : décide de l'alignement et, pour <see cref="Share"/>, de la barre affichée.</summary>
public enum ReportColumnKind
{
    Text,

    /// <summary>Valeur numérique : alignée à droite.</summary>
    Number,

    /// <summary>Part d'un total : affichée comme une barre proportionnelle suivie du pourcentage.</summary>
    Share,
}

public sealed record ReportColumn(string Label, ReportColumnKind Kind = ReportColumnKind.Text);

/// <summary>
/// Une ligne du tableau. <paramref name="IsTotal"/> la met en valeur (ligne de total) et la place
/// telle quelle dans les exports : c'est au module de la construire, l'hôte n'additionne rien
/// lui-même — seul le rapport sait ce que totaliser veut dire pour lui.
/// </summary>
public sealed record ReportRow(IReadOnlyList<ReportCell> Cells, bool IsTotal = false);

/// <summary>
/// Une cellule : son texte, éventuellement la part qu'elle représente (colonne
/// <see cref="ReportColumnKind.Share"/>) et un lien vers la fiche ou la liste correspondante.
/// <see cref="Href"/> est ignoré par les exports, qui n'ont pas de lien à offrir.
/// </summary>
public sealed record ReportCell(string Text, double? Share = null, string? Href = null)
{
    /// <summary>Culture d'écriture des nombres affichés : l'UI est en français en dur (voir le README), et
    /// un rapport ne doit pas changer de séparateur décimal selon la culture du serveur qui l'exécute.</summary>
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Valeur absente affichée comme partout ailleurs dans l'UI : un tiret cadratin, jamais une case vide.</summary>
    public static ReportCell Of(string? text) => new(string.IsNullOrWhiteSpace(text) ? "—" : text);

    public static ReportCell Of(int value) => new(value.ToString("N0", Fr));

    public static ReportCell Link(string? text, string href) => new(string.IsNullOrWhiteSpace(text) ? "—" : text, Href: href);

    /// <summary>
    /// Part de <paramref name="part"/> dans <paramref name="whole"/>. Un total nul donne 0 % plutôt
    /// qu'une division par zéro : un parc vide est un cas courant (première installation), pas une
    /// erreur.
    /// </summary>
    public static ReportCell Percent(double part, double whole)
    {
        double ratio = whole <= 0 ? 0 : part / whole;
        return new ReportCell(ratio.ToString("P1", Fr), ratio);
    }

    public static ReportCell Date(DateTime? value)
        => new(value is { } date ? date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Fr) : "—");
}
