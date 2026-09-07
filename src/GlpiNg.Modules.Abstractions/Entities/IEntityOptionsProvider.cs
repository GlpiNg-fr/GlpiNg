namespace GlpiNg.Modules.Abstractions.Entities;

/// <summary>Projection minimale d'une entité pour un sélecteur : identifiant et nom affiché.</summary>
public sealed record EntityOption(int Id, string Name);

/// <summary>
/// Donne aux formulaires des modules la liste des entités auxquelles l'utilisateur courant peut
/// rattacher un objet. Implémenté par l'hôte : <c>GlpiEntity</c> vit dans <c>GlpiNg.Web.Models</c>,
/// que les modules ne référencent pas — même principe que
/// <see cref="Deployment.IDeploymentTargetDirectory"/>.
///
/// La liste est restreinte au cloisonnement de l'utilisateur : on ne peut pas déplacer un objet
/// vers une entité qu'on ne voit pas, sinon on le ferait disparaître de sa propre vue sans pouvoir
/// revenir en arrière.
/// </summary>
public interface IEntityOptionsProvider
{
    Task<IReadOnlyList<EntityOption>> GetAssignableAsync(CancellationToken cancellationToken = default);
}
