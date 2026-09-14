namespace GlpiNg.Modules.Abstractions.Reports;

/// <summary>
/// Implémenté par un module pour contribuer des rapports à <c>/tools/reports</c>, comme
/// <see cref="Menu.IMenuProvider"/> contribue des entrées de menu : l'hôte résout
/// <see cref="IEnumerable{T}"/> de IReportProvider, concatène les
/// <see cref="ReportDefinition"/> obtenues et confie l'exécution au fournisseur qui reconnaît la
/// clé demandée. Aucun rapport n'est donc câblé dans l'hôte — un module qui arrive apporte les
/// siens, un module désactivé emporte les siens avec lui.
///
/// À enregistrer en <c>Scoped</c> (ex. <c>services.AddScoped&lt;IReportProvider, ...&gt;()</c>
/// dans l'extension <c>AddXxxModule</c>) : un rapport lit la base, donc le cloisonnement par
/// entité de l'utilisateur courant — qui est une notion de scope — doit s'appliquer.
/// </summary>
public interface IReportProvider
{
    IReadOnlyList<ReportDefinition> GetReports();

    /// <summary>
    /// Critères de saisie du rapport <paramref name="reportKey"/>, options comprises (voir
    /// <see cref="ReportFilter"/>). Liste vide pour un rapport sans filtre, et pour une clé que ce
    /// fournisseur ne connaît pas.
    /// </summary>
    Task<IReadOnlyList<ReportFilter>> GetFiltersAsync(string reportKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute le rapport, ou renvoie <c>null</c> si <paramref name="reportKey"/> n'est pas l'un
    /// des siens — c'est ce qui permet à l'hôte d'interroger les fournisseurs à la suite sans
    /// tenir lui-même la table des clés.
    /// </summary>
    Task<ReportResult?> RunAsync(string reportKey, ReportParameters parameters, CancellationToken cancellationToken = default);
}
