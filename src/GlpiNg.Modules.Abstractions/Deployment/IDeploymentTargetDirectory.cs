namespace GlpiNg.Modules.Abstractions.Deployment;

/// <summary>
/// Implémenté par l'hôte (GlpiNg.Web) pour fournir au module Déploiement les listes
/// d'options utilisables par l'onglet "Cibles pour le déploiement à la demande" (voir
/// Components/Pages/Deployments/Detail.razor.cs) — Entité/Groupe/Profil/Utilisateur vivent
/// tous dans GlpiNg.Web.Models (domaine utilisateurs/groupes/entités/profils, pas encore
/// extrait en module), que GlpiNg.Modules.Deployment ne peut pas référencer directement.
/// Même principe que <see cref="IComputerDeploymentTasksProvider"/>, sens inversé : ici
/// c'est Deployment qui dépend seulement de cette abstraction, jamais de GlpiNg.Web.
/// </summary>
public interface IDeploymentTargetDirectory
{
    Task<IReadOnlyList<DeploymentTargetOption>> GetEntitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeploymentTargetOption>> GetGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeploymentTargetOption>> GetProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeploymentTargetOption>> GetUsersAsync(CancellationToken cancellationToken = default);
}

/// <summary>Projection minimale (id + nom affiché) d'une Entité/Groupe/Profil/Utilisateur pour affichage/sélection.</summary>
public sealed record DeploymentTargetOption(int Id, string Name);
