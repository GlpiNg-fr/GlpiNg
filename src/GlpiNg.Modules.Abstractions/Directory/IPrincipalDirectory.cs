namespace GlpiNg.Modules.Abstractions.Directory;

/// <summary>
/// Nature d'un « acteur » désignable : les quatre mêmes que GLPI propose partout où l'on restreint
/// la visibilité d'un objet (entité, groupe, profil, utilisateur).
/// </summary>
public enum PrincipalKind
{
    Entity,
    Group,
    Profile,
    User,
}

/// <summary>Projection minimale (identifiant + nom affiché) d'un acteur, pour un sélecteur.</summary>
public sealed record PrincipalOption(int Id, string Name);

/// <summary>
/// Implémenté par l'hôte (GlpiNg.Web) pour donner aux modules les listes d'entités, groupes,
/// profils et utilisateurs : ces quatre types vivent dans <c>GlpiNg.Web.Models</c> (domaine
/// utilisateurs/groupes/entités/profils, pas encore extrait en module), qu'un module ne peut pas
/// référencer sans dépendre de l'hôte.
///
/// Généralise <see cref="Deployment.IDeploymentTargetDirectory"/>, qui rendait déjà ce service au
/// seul module Déploiement : les deux sont rendus par la même implémentation hôte, l'ancien
/// contrat restant en place pour ne pas réécrire le module qui s'en sert. Un module nouveau
/// (Base de connaissances, par exemple) s'adresse à celui-ci.
/// </summary>
public interface IPrincipalDirectory
{
    Task<IReadOnlyList<PrincipalOption>> GetAsync(PrincipalKind kind, CancellationToken cancellationToken = default);
}
