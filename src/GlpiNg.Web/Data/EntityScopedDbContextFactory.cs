using GlpiNg.Modules.Abstractions.Entities;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Data;

/// <summary>
/// Fabrique de contextes cloisonnés : chaque contexte produit est estampillé avec le
/// <see cref="EntityScope"/> de l'utilisateur courant, ce qui active les filtres globaux posés par
/// <c>GlpiNgDbContext.ApplyEntityScoping</c>.
///
/// C'est elle qui est enregistrée sous <see cref="IDbContextFactory{TContext}"/>, donc les ~97
/// fichiers de pages qui font déjà <c>DbFactory.CreateDbContextAsync()</c> deviennent cloisonnés
/// sans une seule modification — et surtout sans qu'un oubli de filtre soit possible.
/// Enregistrée en <c>Scoped</c> : le cloisonnement dépend de l'utilisateur, donc du circuit Blazor
/// ou de la requête HTTP. Les singletons passent par <see cref="IRootDbContextFactory"/>.
///
/// Hors session applicative (protocole agent, tâches cron, imports machine-à-machine),
/// <see cref="IEntityScopeProvider"/> renvoie <see cref="EntityScope.Unrestricted"/> : ces
/// chemins continuent donc de tout voir, même en passant par cette fabrique.
/// </summary>
internal sealed class EntityScopedDbContextFactory(
    DbContextOptions<GlpiNgDbContext> options,
    IEntityScopeProvider scopeProvider) : IDbContextFactory<GlpiNgDbContext>
{
    public GlpiNgDbContext CreateDbContext() => new(options) { EntityScope = scopeProvider.Current };

    // Implémentation explicite : si la signature de l'interface changeait (Task/ValueTask), le
    // compilateur le signalerait ici, au lieu de laisser une surcharge muette à côté de
    // l'implémentation par défaut de l'interface — qui court-circuiterait l'estampillage.
    Task<GlpiNgDbContext> IDbContextFactory<GlpiNgDbContext>.CreateDbContextAsync(CancellationToken cancellationToken)
        => Task.FromResult(CreateDbContext());
}
