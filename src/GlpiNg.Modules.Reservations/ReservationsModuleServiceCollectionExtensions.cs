using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Abstractions.Menu;
using GlpiNg.Modules.Abstractions.Reports;
using GlpiNg.Modules.Abstractions.Reservations;
using GlpiNg.Modules.Reservations.Reports;
using GlpiNg.Modules.Reservations.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GlpiNg.Modules.Reservations;

/// <summary>
/// Point d'enregistrement du module Réservations dans le conteneur DI de l'hôte, même principe que
/// <c>KnowledgeBaseModuleServiceCollectionExtensions.AddKnowledgeBaseModule</c>.
///
/// Ce module n'expose aucun contrôleur : pas d'appel à <c>AddApplicationPart</c>. Ses pages Razor
/// vivent dans une autre assembly que l'hôte et doivent être déclarées au routeur — voir
/// <c>AdditionalAssemblies</c> dans Routes.razor.
///
/// Il dépend du <c>DbContext</c> de base : à appeler après que l'hôte a enregistré son DbContext
/// concret, comme les autres modules (voir Program.cs).
/// </summary>
public static class ReservationsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddReservationsModule(this IServiceCollection services)
    {
        // Les règles des réservations, utilisées à la fois par les pages du module et, via
        // IItemReservations, par l'onglet « Réservations » des fiches des autres modules.
        services.AddScoped<ReservationService>();
        services.AddScoped<IItemReservations>(sp => sp.GetRequiredService<ReservationService>());

        // Contribution du module au menu latéral de l'hôte (entrée « Réservations » du groupe
        // « Outils ») — voir ReservationsMenuProvider.
        services.AddSingleton<IMenuProvider, ReservationsMenuProvider>();

        // Contribution du module aux rapports de l'hôte (/tools/reports).
        services.AddScoped<IReportProvider, ReservationReportProvider>();

        // Rappel d'échéance, exécuté par le service cron de l'hôte et réglable depuis
        // /config/automatic-actions comme toute autre action automatique.
        services.AddScoped<ICronTask, ReservationAlertCronTask>();

        return services;
    }
}
