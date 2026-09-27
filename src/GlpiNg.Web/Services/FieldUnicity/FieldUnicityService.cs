using System.Linq.Expressions;
using System.Reflection;
using GlpiNg.Modules.Abstractions.FieldUnicity;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.FieldUnicity;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Services.Notifications;
using Microsoft.EntityFrameworkCore;
using GlpiNg.Modules.Abstractions.Localization;

namespace GlpiNg.Web.Services.FieldUnicity;

/// <summary>
/// Applique les critères d'unicité des champs (voir <see cref="FieldUnicityCriterion"/>) — pendant
/// de <c>FieldUnicity::checkUnicity</c> dans GLPI, appelé avant la création d'un objet.
///
/// Un champ laissé vide ne participe pas au contrôle, et un critère dont aucun champ n'est
/// renseigné ne s'applique pas : sans cela, deux actifs sans numéro de série se verraient comme
/// doublons l'un de l'autre, et le premier import en masse d'un parc se bloquerait tout seul.
/// </summary>
public sealed class FieldUnicityService(
    IDbContextFactory<GlpiNgDbContext> dbFactory,
    NotificationDispatchService notificationDispatch,
    ILogger<FieldUnicityService> logger) : IFieldUnicityChecker
{
    public async Task<FieldUnicityVerdict> CheckAsync<TItem>(
        string itemType,
        IQueryable<TItem> existing,
        TItem candidate,
        int? excludedId = null,
        CancellationToken cancellationToken = default) where TItem : class
    {
        await using GlpiNgDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);

        List<FieldUnicityCriterion> criteria = await db.FieldUnicityCriteria
            .AsNoTracking()
            .Include(criterion => criterion.Fields)
            .Where(criterion => criterion.ItemType == itemType && criterion.IsActive)
            .OrderBy(criterion => criterion.Name)
            .ToListAsync(cancellationToken);

        FieldUnicityVerdict verdict = FieldUnicityVerdict.Allowed;

        foreach (FieldUnicityCriterion criterion in criteria)
        {
            List<(string Field, string Value)> pairs = ReadCandidateValues(criterion, candidate);

            if (pairs.Count == 0)
            {
                continue;
            }

            bool duplicate = await existing
                .Where(BuildDuplicatePredicate<TItem>(pairs, excludedId))
                .AnyAsync(cancellationToken);

            if (!duplicate)
            {
                continue;
            }

            logger.LogInformation(
                "unicité des champs : doublon {ItemType} détecté par le critère « {Criterion} » sur {Fields}.",
                itemType, criterion.Name, string.Join(", ", pairs.Select(pair => pair.Field)));

            if (criterion.NotifyOnDuplicate)
            {
                await PublishDuplicateAsync(criterion, itemType, pairs, cancellationToken);
            }

            // Le premier critère qui refuse l'emporte, mais l'examen continue : un autre critère
            // peut encore demander une notification, qui doit partir même si la création est déjà
            // condamnée.
            if (criterion.RefuseCreation && !verdict.Refused)
            {
                verdict = new FieldUnicityVerdict(true, criterion.Name, BuildRefusalMessage(criterion, itemType, pairs));
            }
        }

        return verdict;
    }

    /// <summary>
    /// Valeurs du candidat pour les champs du critère, les vides écartés. La lecture se fait par
    /// réflexion sur l'objet en mémoire (et non en base) : c'est un seul objet, et l'appelant n'a
    /// ainsi rien à décrire de son modèle.
    /// </summary>
    private static List<(string Field, string Value)> ReadCandidateValues<TItem>(FieldUnicityCriterion criterion, TItem candidate)
    {
        List<(string Field, string Value)> pairs = [];

        foreach (FieldUnicityField field in criterion.Fields)
        {
            PropertyInfo? property = typeof(TItem).GetProperty(field.FieldName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property?.GetValue(candidate) is string value && !string.IsNullOrWhiteSpace(value))
            {
                pairs.Add((property.Name, value));
            }
        }

        return pairs;
    }

    /// <summary>
    /// « item =&gt; EF.Property&lt;string&gt;(item, champ1) == valeur1 &amp;&amp; ... &amp;&amp; EF.Property&lt;int&gt;(item, "Id") != exclu ».
    ///
    /// Construit à la volée parce que les champs ne sont connus qu'à l'exécution, mais bien traduit
    /// en SQL : le contrôle reste un <c>EXISTS</c> sur la table, jamais un chargement complet pour
    /// comparer en mémoire.
    /// </summary>
    private static Expression<Func<TItem, bool>> BuildDuplicatePredicate<TItem>(
        IReadOnlyList<(string Field, string Value)> pairs, int? excludedId)
    {
        ParameterExpression item = Expression.Parameter(typeof(TItem), "item");
        Expression? body = null;

        foreach ((string field, string value) in pairs)
        {
            MethodCallExpression property = Expression.Call(
                typeof(EF), nameof(EF.Property), [typeof(string)], item, Expression.Constant(field));

            BinaryExpression comparison = Expression.Equal(property, Expression.Constant(value, typeof(string)));
            body = body is null ? comparison : Expression.AndAlso(body, comparison);
        }

        if (excludedId is { } id)
        {
            MethodCallExpression identifier = Expression.Call(
                typeof(EF), nameof(EF.Property), [typeof(int)], item, Expression.Constant("Id"));

            body = Expression.AndAlso(body!, Expression.NotEqual(identifier, Expression.Constant(id)));
        }

        return Expression.Lambda<Func<TItem, bool>>(body!, item);
    }

    private async Task PublishDuplicateAsync(
        FieldUnicityCriterion criterion, string itemType, IReadOnlyList<(string Field, string Value)> pairs,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string?> variables = new(StringComparer.Ordinal)
        {
            ["unicity.criterion"] = criterion.Name,
            ["unicity.itemtype"] = FieldUnicityCatalog.LabelFor(itemType),
            ["unicity.fields"] = string.Join(", ", pairs.Select(pair => FieldUnicityCatalog.LabelForField(pair.Field))),
            ["unicity.values"] = string.Join(", ", pairs.Select(pair => pair.Value)),
            ["unicity.refused"] = criterion.RefuseCreation ? "oui" : "non",
        };

        await notificationDispatch.PublishAsync(
            NotificationEventCatalog.FieldUnicity, NotificationEventCatalog.EventDuplicate, criterion.Id,
            variables, cancellationToken);
    }

    private static string BuildRefusalMessage(
        FieldUnicityCriterion criterion, string itemType, IReadOnlyList<(string Field, string Value)> pairs)
    {
        string fields = string.Join(", ", pairs.Select(pair =>
            $"{FieldUnicityCatalog.LabelForField(pair.Field)} « {pair.Value} »"));

        return Tr.T("Création refusée : un autre élément de type {0} porte déjà ", FieldUnicityCatalog.LabelFor(itemType))
               + $"{fields} (critère d'unicité « {criterion.Name} »).";
    }
}
