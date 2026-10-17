using GlpiNg.Modules.Abstractions.Menu;

namespace GlpiNg.Modules.Reservations;

/// <summary>
/// Contribue l'entrée « Réservations » du groupe « Outils », que l'hôte affichait jusqu'ici
/// désactivée faute de page derrière — même montage que <c>KnowledgeBaseMenuProvider</c>, dont
/// l'entrée du même groupe est fusionnée avec celle-ci.
///
/// Une seule entrée : l'administration des éléments réservables s'atteint depuis le planning, et
/// la seconde entrée de menu que GLPI n'a pas non plus n'apporterait rien.
/// </summary>
public sealed class ReservationsMenuProvider : IMenuProvider
{
    public IReadOnlyList<MenuGroup> GetMenuGroups() =>
    [
        new("outils", "ti-briefcase", "Outils",
        [
            new("Réservations", "/tools/reservations", "ti-calendar-event"),
        ]),
    ];
}
