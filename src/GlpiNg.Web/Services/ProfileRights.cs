using GlpiNg.Web.Models;

namespace GlpiNg.Web.Services;

/// <summary>
/// Grande section du menu sur laquelle porte un droit de <see cref="GlpiProfile"/>. Les valeurs
/// reprennent une par une les clés de groupe du menu latéral (<c>MainLayout.GroupOrder</c>) et les
/// six colonnes de droits du profil : c'est ce qui permet de filtrer le menu, les routes et les
/// écritures avec une seule notion.
/// </summary>
public enum ProfileSection
{
    Parc,
    Assistance,
    Gestion,
    Outils,
    Administration,
    Configuration,
}

/// <summary>
/// Droits effectifs de l'utilisateur courant, une valeur par section. « Effectifs » parce qu'ils
/// sont résolus pour l'entité active : un utilisateur peut être gestionnaire dans une entité et
/// simple lecteur dans une autre, donc changer d'entité active change les droits (voir
/// <c>AccountController.SwitchEntity</c>, qui les recalcule).
/// </summary>
public sealed class ProfileRights
{
    /// <summary>Tous les droits en écriture : compte administrateur, et chemins hors session
    /// applicative (protocole agent, cron, import machine-à-machine).</summary>
    public static readonly ProfileRights Full = new(_ => ProfileRightLevel.Write);

    /// <summary>Aucun droit : utilisateur connecté sans habilitation sur l'entité active.</summary>
    public static readonly ProfileRights None = new(_ => ProfileRightLevel.None);

    private readonly Dictionary<ProfileSection, ProfileRightLevel> _levels;

    public ProfileRights(Func<ProfileSection, ProfileRightLevel> levelFor)
        => _levels = Enum.GetValues<ProfileSection>().ToDictionary(section => section, levelFor);

    public ProfileRightLevel Level(ProfileSection section) => _levels.GetValueOrDefault(section);

    public bool CanRead(ProfileSection section) => Level(section) >= ProfileRightLevel.Read;

    public bool CanWrite(ProfileSection section) => Level(section) >= ProfileRightLevel.Write;

    /// <summary>
    /// Le droit sur une section inconnue ne bloque rien : les pages transverses (tableau de bord,
    /// profil, préférences) n'appartiennent à aucune section, et une section non reconnue ne doit
    /// pas rendre l'application inutilisable.
    /// </summary>
    public bool CanRead(ProfileSection? section) => section is null || CanRead(section.Value);

    public bool CanWrite(ProfileSection? section) => section is null || CanWrite(section.Value);
}

/// <summary>
/// Donne les droits applicables au contexte courant. Même forme et mêmes garanties que
/// <see cref="Modules.Abstractions.Entities.IEntityScopeProvider"/> : enregistré en
/// <c>Scoped</c>, mémorisé pour la durée du scope, et neutre (<see cref="ProfileRights.Full"/>)
/// hors session applicative.
/// </summary>
public interface IProfileRightsProvider
{
    ProfileRights Current { get; }
}
