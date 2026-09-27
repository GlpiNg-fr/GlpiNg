using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Data;

/// <summary>
/// Fabrique de contextes <b>non cloisonnés</b> : les contextes qu'elle produit voient toutes les
/// entités, quel que soit l'utilisateur connecté.
///
/// Existe parce que le cloisonnement est porté par une fabrique <c>Scoped</c>
/// (<see cref="EntityScopedDbContextFactory"/>) que, par construction, un service singleton ne
/// peut pas consommer. Les deux usages légitimes sont donc :
/// <list type="bullet">
///   <item>les services singleton (<c>SettingsCacheService</c>, <c>LdapAuthenticationService</c>),
///   qui n'ont pas d'utilisateur courant ;</item>
///   <item>le calcul du cloisonnement lui-même (<c>EntityTreeCache</c>) — il lui faut lire
///   l'arbre des entités avant de pouvoir savoir quoi filtrer.</item>
/// </list>
///
/// Toute autre injection de ce type est à considérer comme un contournement du cloisonnement :
/// les pages et les services scoped doivent prendre <see cref="IDbContextFactory{TContext}"/>.
/// </summary>
public interface IRootDbContextFactory
{
    GlpiNgDbContext CreateDbContext();

    /// <summary>
    /// Pendant asynchrone, pour rester interchangeable avec
    /// <see cref="IDbContextFactory{TContext}"/> sur les sites d'appel. La construction du
    /// contexte est synchrone : il n'y a rien à attendre, c'est l'ouverture de connexion (au
    /// premier accès) qui l'est.
    /// </summary>
    ValueTask<GlpiNgDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(CreateDbContext());
}

/// <summary>
/// Construit le contexte directement depuis les <see cref="DbContextOptions{TContext}"/>
/// enregistrés en singleton par <c>AddDbContextFactory</c>, plutôt qu'en déléguant à
/// <see cref="IDbContextFactory{TContext}"/> : ce dernier service est justement remplacé par la
/// fabrique cloisonnée, qui est scoped.
/// </summary>
internal sealed class RootDbContextFactory(DbContextOptions<GlpiNgDbContext> options) : IRootDbContextFactory
{
    public GlpiNgDbContext CreateDbContext() => new(options);
}
