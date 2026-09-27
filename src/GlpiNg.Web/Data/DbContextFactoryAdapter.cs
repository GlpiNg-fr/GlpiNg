using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Data;

/// <summary>
/// Adapte l'<see cref="IDbContextFactory{GlpiNgDbContext}"/> concret de l'hôte en
/// <see cref="IDbContextFactory{DbContext}"/>, pour que les composants Razor des modules (ex.
/// GlpiNg.Modules.Inventory), qui ne connaissent que le DbContext de base, puissent en obtenir
/// une instance courte durée par opération — même raison de concurrence que documentée sur
/// l'enregistrement de IDbContextFactory&lt;GlpiNgDbContext&gt; dans Program.cs.
/// </summary>
internal sealed class DbContextFactoryAdapter(IDbContextFactory<GlpiNgDbContext> inner) : IDbContextFactory<DbContext>
{
    public DbContext CreateDbContext() => inner.CreateDbContext();

    // Implémentation explicite, et Task plutôt que ValueTask : la signature de l'interface est
    // Task<TContext>: CreateDbContextAsync. Déclarée en ValueTask, la méthode n'implémentait pas
    // le membre — les appels passaient par l'implémentation par défaut de l'interface. Sans
    // conséquence tant que celle-ci délègue à CreateDbContext, mais c'est exactement le genre de
    // méthode muette qui laisserait passer un contexte non estampillé (voir
    // EntityScopedDbContextFactory).
    async Task<DbContext> IDbContextFactory<DbContext>.CreateDbContextAsync(CancellationToken cancellationToken) =>
        await inner.CreateDbContextAsync(cancellationToken);
}
