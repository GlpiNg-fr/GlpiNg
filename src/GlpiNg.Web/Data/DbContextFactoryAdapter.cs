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

    public async ValueTask<DbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        await inner.CreateDbContextAsync(cancellationToken);
}
