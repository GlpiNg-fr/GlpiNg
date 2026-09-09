namespace GlpiNg.Modules.Abstractions.Preferences;

/// <summary>
/// Préférences d'affichage de l'utilisateur connecté, telles que les pages des modules peuvent les
/// consulter sans connaître le modèle de compte de l'hôte.
/// </summary>
/// <param name="ItemsPerPage">Nombre de lignes affichées par défaut dans les tableaux paginés.</param>
/// <param name="MacAddressFormat">
/// Écriture des adresses MAC, déjà résolue : jamais <see cref="Preferences.MacAddressFormat.Default"/>.
/// Le choix personnel s'il existe, sinon le réglage de l'instance — l'appelant n'a pas à connaître
/// cette cascade pour afficher une adresse.
/// </param>
public sealed record UserPreferenceValues(int ItemsPerPage, MacAddressFormat MacAddressFormat)
{
    /// <summary>Valeurs appliquées hors session applicative, et tant que le compte n'a rien choisi.</summary>
    public static readonly UserPreferenceValues Defaults = new(25, MacAddressFormatter.Fallback);

    public string? FormatMac(string? mac) => MacAddressFormatter.Format(mac, MacAddressFormat);
}

/// <summary>
/// Donne accès aux préférences du compte connecté. Implémenté par l'hôte (les préférences sont
/// portées par son modèle d'utilisateur), consommé par les pages des modules — même montage que
/// IComputerDeploymentTasksProvider.
///
/// Asynchrone et non une simple propriété : la valeur vient de la base, pas des revendications de
/// la session. Un réglage déposé dans le cookie à la connexion ne changerait qu'au prochain login,
/// ce qui est exactement ce qu'un écran de préférences ne doit pas faire. L'implémentation met le
/// résultat en cache pour la durée du circuit : une lecture, pas une par tableau.
/// </summary>
public interface IUserPreferences
{
    ValueTask<UserPreferenceValues> GetAsync(CancellationToken cancellationToken = default);
}
