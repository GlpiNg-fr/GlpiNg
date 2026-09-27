using System.Text.Json.Nodes;

namespace GlpiNg.Web.Api.Glpi.Data;

/// <summary>Ligne d'une table GLPI : colonne → valeur GLPI (entier, texte, date UTC, booléen...).</summary>
public sealed class GlpiRow : Dictionary<string, object?>
{
    public GlpiRow() : base(StringComparer.Ordinal)
    {
    }

    public int Id => GlpiValue.TryToLong(this.GetValueOrDefault("id"), out long id) ? (int)id : 0;
}

/// <summary>Données d'écriture reçues par l'API : colonne GLPI → valeur JSON.</summary>
public sealed class GlpiInput : Dictionary<string, JsonNode?>
{
    public GlpiInput() : base(StringComparer.Ordinal)
    {
    }

    public GlpiInput(IDictionary<string, JsonNode?> source) : base(source, StringComparer.Ordinal)
    {
    }
}

/// <summary>Échec d'une écriture, avec le message que GLPI renverrait au client.</summary>
public sealed class GlpiWriteException(string message, bool isRightError = false) : Exception(message)
{
    public bool IsRightError { get; } = isRightError;

    public const string PermissionMessage = "You don't have permission to perform this action.";

    public static GlpiWriteException Right() => new(PermissionMessage, isRightError: true);
}

/// <summary>Une page de résultats, avec le nombre total de lignes répondant à la condition.</summary>
public sealed record GlpiPage(IReadOnlyList<int> Ids, int Total);

/// <summary>
/// Table GLPI exposée par GlpiNg (<c>glpi_computers</c>, <c>glpi_tickets</c>...) : mêmes colonnes,
/// mêmes identifiants, mêmes conventions (0 = racine, 0 = aucune valeur d'intitulé), alimentées par
/// le modèle GlpiNg. C'est la couche qui permet aux deux API de GLPI de fonctionner telles que GLPI
/// les implémente, par-dessus des tables qui ne sont pas les siennes.
///
/// Le cloisonnement par entité et les droits de l'utilisateur s'appliquent à toutes les lectures
/// et écritures : elles passent par le contexte EF de la requête.
/// </summary>
public interface IGlpiTable
{
    /// <summary>Nom de la table GLPI.</summary>
    string Name { get; }

    /// <summary>Type du modèle GlpiNg qui l'alimente, pour les droits par section ; null pour une table vide.</summary>
    Type? ClrType { get; }

    /// <summary>Vrai si la table accepte les écritures.</summary>
    bool IsWritable { get; }

    /// <summary>Vrai si la suppression peut mettre en corbeille (<c>is_deleted</c>) plutôt que purger.</summary>
    bool HasTrash { get; }

    /// <summary>Vrai si la colonne est alimentée par le modèle (et non une valeur par défaut de GLPI).</summary>
    bool IsMapped(string column);

    /// <summary>Identifiants des lignes visibles répondant à la condition, triées et paginées.</summary>
    Task<GlpiPage> QueryAsync(GlpiDataContext ctx, GlpiFilter? filter, IReadOnlyList<GlpiSort>? sort, int start, int? limit, CancellationToken ct);

    /// <summary>Lignes complètes (colonnes alimentées) pour les identifiants donnés, parmi celles visibles.</summary>
    Task<Dictionary<int, GlpiRow>> GetRowsAsync(GlpiDataContext ctx, IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>Valeurs d'une colonne pour les lignes visibles répondant à la condition.</summary>
    Task<Dictionary<int, object?>> GetColumnAsync(GlpiDataContext ctx, string column, GlpiFilter? filter, CancellationToken ct);

    Task<int> InsertAsync(GlpiDataContext ctx, GlpiInput input, CancellationToken ct);

    Task UpdateAsync(GlpiDataContext ctx, int id, GlpiInput input, CancellationToken ct);

    Task DeleteAsync(GlpiDataContext ctx, int id, bool purge, CancellationToken ct);

    Task RestoreAsync(GlpiDataContext ctx, int id, CancellationToken ct);
}

/// <summary>
/// Table GLPI sans équivalent dans GlpiNg : elle existe (l'itemtype est valide, ses colonnes et
/// ses options de recherche sont celles de GLPI) mais ne contient aucune ligne — exactement ce
/// que répondrait une instance GLPI où rien n'a été saisi pour ce type.
/// </summary>
public sealed class EmptyGlpiTable(string name) : IGlpiTable
{
    public string Name { get; } = name;

    public Type? ClrType => null;

    public bool IsWritable => false;

    public bool HasTrash => false;

    public bool IsMapped(string column) => false;

    public Task<GlpiPage> QueryAsync(GlpiDataContext ctx, GlpiFilter? filter, IReadOnlyList<GlpiSort>? sort, int start, int? limit, CancellationToken ct)
        => Task.FromResult(new GlpiPage([], 0));

    public Task<Dictionary<int, GlpiRow>> GetRowsAsync(GlpiDataContext ctx, IReadOnlyCollection<int> ids, CancellationToken ct)
        => Task.FromResult(new Dictionary<int, GlpiRow>());

    public Task<Dictionary<int, object?>> GetColumnAsync(GlpiDataContext ctx, string column, GlpiFilter? filter, CancellationToken ct)
        => Task.FromResult(new Dictionary<int, object?>());

    public Task<int> InsertAsync(GlpiDataContext ctx, GlpiInput input, CancellationToken ct)
        => throw new GlpiWriteException($"GlpiNg ne gère pas encore les éléments de la table {Name}.");

    public Task UpdateAsync(GlpiDataContext ctx, int id, GlpiInput input, CancellationToken ct)
        => throw new GlpiWriteException("Item not found");

    public Task DeleteAsync(GlpiDataContext ctx, int id, bool purge, CancellationToken ct)
        => throw new GlpiWriteException("Item not found");

    public Task RestoreAsync(GlpiDataContext ctx, int id, CancellationToken ct)
        => throw new GlpiWriteException("Item not found");
}

/// <summary>Évaluation en mémoire d'une condition sur une ligne déjà chargée.</summary>
public static class GlpiFilterEvaluator
{
    public static bool Matches(GlpiRow row, GlpiFilter? filter) => filter switch
    {
        null => true,
        GlpiConst c => c.Value,
        GlpiAnd a => a.Items.All(i => Matches(row, i)),
        GlpiOr o => o.Items.Any(i => Matches(row, i)),
        GlpiNot n => !Matches(row, n.Inner),
        GlpiColumnFilter c => Matches(row.GetValueOrDefault(c.Column), c.Op, c.Value),
        _ => false,
    };

    public static bool Matches(object? actual, GlpiOp op, object? expected)
    {
        switch (op)
        {
            case GlpiOp.IsNull:
                return actual is null;
            case GlpiOp.NotNull:
                return actual is not null;
            case GlpiOp.Eq:
                return expected is null ? actual is null : actual is not null && GlpiValue.AreEqual(actual, expected);
            case GlpiOp.Ne:
                return expected is null ? actual is not null : actual is null || !GlpiValue.AreEqual(actual, expected);
            case GlpiOp.Lt:
                return actual is not null && GlpiValue.Compare(actual, expected) < 0;
            case GlpiOp.Le:
                return actual is not null && GlpiValue.Compare(actual, expected) <= 0;
            case GlpiOp.Gt:
                return actual is not null && GlpiValue.Compare(actual, expected) > 0;
            case GlpiOp.Ge:
                return actual is not null && GlpiValue.Compare(actual, expected) >= 0;
            case GlpiOp.Like:
                return GlpiValue.Like(actual, GlpiValue.ToText(expected) ?? string.Empty);
            case GlpiOp.NotLike:
                return actual is not null && !GlpiValue.Like(actual, GlpiValue.ToText(expected) ?? string.Empty);
            case GlpiOp.In:
                return expected is System.Collections.IEnumerable list and not string
                       && list.Cast<object?>().Any(v => GlpiValue.AreEqual(actual, v));
            case GlpiOp.NotIn:
                return !Matches(actual, GlpiOp.In, expected);
            default:
                return false;
        }
    }
}
