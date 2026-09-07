namespace GlpiNg.Modules.Abstractions.Entities;

/// <summary>
/// Donne le cloisonnement applicable au contexte courant (circuit Blazor ou requête HTTP).
/// Enregistré en <c>Scoped</c> : il n'y a pas de « scope courant » global, et un service
/// singleton n'a par construction pas d'utilisateur — voir <c>IRootDbContextFactory</c> pour
/// les accès délibérément non cloisonnés.
///
/// Renvoie <see cref="EntityScope.Unrestricted"/> dès qu'il n'y a pas de session applicative :
/// protocole agent (<c>/inventory</c>), tâches cron, imports machine-à-machine. C'est ce qui
/// permet aux filtres globaux d'être appliqués partout sans casser ces chemins.
/// </summary>
public interface IEntityScopeProvider
{
    /// <summary>Cloisonnement courant. Calculé une fois par scope puis mémorisé.</summary>
    EntityScope Current { get; }
}
