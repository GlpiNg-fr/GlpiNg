namespace GlpiNg.Modules.Abstractions.FieldUnicity;

/// <summary>
/// Verdict rendu par <see cref="IFieldUnicityChecker"/> pour un objet sur le point d'être créé.
///
/// Un critère qui correspond sans refuser (il ne fait que notifier) rend un verdict autorisé : la
/// notification part, mais l'appelant n'a rien à faire. C'est ce qui permet à un appelant de se
/// contenter de « si refusé, afficher le message et ne rien créer », sans rien connaître des
/// critères configurés.
/// </summary>
/// <param name="Refused">Vrai si un critère correspondant demande le refus de la création.</param>
/// <param name="CriterionName">Nom du critère ayant refusé, pour en garder trace côté appelant.</param>
/// <param name="Message">Message prêt à afficher, renseigné seulement en cas de refus.</param>
public sealed record FieldUnicityVerdict(bool Refused, string? CriterionName, string? Message)
{
    public static readonly FieldUnicityVerdict Allowed = new(false, null, null);
}

/// <summary>
/// Contrôle des critères d'unicité des champs (« Configuration &gt; Unicité des champs », équivalent
/// de <c>FieldUnicity</c> dans GLPI) : avant de créer un objet, l'appelant demande si un objet du
/// même type porte déjà les mêmes valeurs sur les champs déclarés uniques par un administrateur.
///
/// Implémenté par l'hôte (qui seul porte la table des critères) et consommé par les modules, sur le
/// même montage que <c>IDocumentAttachments</c> ou <c>IItemNotes</c> : un module dépend du contrat,
/// jamais de <c>GlpiNg.Web</c>.
///
/// L'appelant fournit sa propre requête sur le type concerné, ce qui évite à l'hôte d'aller
/// chercher dynamiquement une table qu'il ne connaît pas, et fait bénéficier le contrôle des
/// filtres globaux déjà posés dessus — le cloisonnement par entité, notamment : un doublon
/// invisible depuis l'entité active ne bloque pas la création, exactement comme dans GLPI où un
/// critère ne vaut que dans son entité.
/// </summary>
public interface IFieldUnicityChecker
{
    /// <param name="itemType">Type d'objet au sens GLPI (« Computer », « Printer », ...).</param>
    /// <param name="existing">Requête sur les objets déjà enregistrés de ce type.</param>
    /// <param name="candidate">Objet sur le point d'être créé ou modifié.</param>
    /// <param name="excludedId">Identifiant à exclure de la recherche, lors d'une modification : sans
    /// lui, l'objet se verrait comme son propre doublon.</param>
    Task<FieldUnicityVerdict> CheckAsync<TItem>(
        string itemType,
        IQueryable<TItem> existing,
        TItem candidate,
        int? excludedId = null,
        CancellationToken cancellationToken = default) where TItem : class;
}
