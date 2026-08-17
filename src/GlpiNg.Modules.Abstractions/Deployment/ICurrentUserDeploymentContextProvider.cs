namespace GlpiNg.Modules.Abstractions.Deployment;

/// <summary>
/// Implémenté par l'hôte (GlpiNg.Web) pour donner au module Déploiement de quoi évaluer
/// l'éligibilité de l'utilisateur connecté au libre-service (page /self-service, voir
/// SelfServiceDeploymentService) sans que ce module dépende de GlpiUser/GlpiUserProfile/
/// GlpiGroupUser/GlpiEntity (domaine utilisateurs, pas encore extrait en module). Résout dans le
/// sens inverse d'<see cref="IDeploymentTargetDirectory"/> : celle-ci traduit un ID de Cible en
/// nom affiché pour la configuration admin (onglet "Cibles" d'un paquet) ; celle-ci traduit un
/// utilisateur connecté en ses habilitations, pour évaluer ces mêmes Cibles côté utilisateur final.
/// </summary>
public interface ICurrentUserDeploymentContextProvider
{
    Task<CurrentUserDeploymentContext?> GetContextAsync(int userId, CancellationToken cancellationToken = default);
}

/// <summary>Habilitations pertinentes d'un utilisateur pour évaluer les Cibles ("Entité"/"Groupe"/
/// "Profil"/"Utilisateur") d'un DeploymentPackage — voir DeploymentPackageTarget.</summary>
public sealed class CurrentUserDeploymentContext
{
    public int UserId { get; init; }
    public required string UserName { get; init; }
    public IReadOnlyList<int> EntityIds { get; init; } = [];
    public IReadOnlyList<int> ProfileIds { get; init; } = [];
    public IReadOnlyList<int> GroupIds { get; init; } = [];
}
