using System.Globalization;

namespace GlpiNg.Modules.Abstractions.Reports;

/// <summary>
/// Valeurs saisies dans les filtres d'un rapport, telles que l'hôte les transmet au module.
///
/// Tout est chaîne parce que tout vient d'un formulaire HTML : les accesseurs typés ci-dessous
/// évitent que chaque rapport refasse le même <c>TryParse</c> défensif, et traitent une valeur
/// absente, vide ou illisible de la même façon — <c>null</c>, c'est-à-dire « pas de filtre ».
/// Un rapport n'a donc jamais à se demander si l'utilisateur a effacé le champ ou ne l'a jamais
/// rempli : les deux veulent dire la même chose.
/// </summary>
public sealed class ReportParameters(IReadOnlyDictionary<string, string?> values)
{
    /// <summary>Aucun filtre saisi : ce que reçoit un rapport exécuté sur ses seules valeurs par défaut.</summary>
    public static readonly ReportParameters Empty = new(new Dictionary<string, string?>());

    public string? GetString(string key)
        => values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    public int? GetInt(string key)
        => int.TryParse(GetString(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : null;

    /// <summary>
    /// Date d'un filtre <see cref="ReportFilterKind.Date"/>. Le format <c>yyyy-MM-dd</c> émis par
    /// <c>&lt;input type="date"&gt;</c> est lu en invariant : la culture du serveur ne doit pas
    /// décider de la lecture d'une valeur que le navigateur produit toujours pareil.
    /// </summary>
    public DateTime? GetDate(string key)
        => DateTime.TryParse(GetString(key), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed) ? parsed : null;
}
