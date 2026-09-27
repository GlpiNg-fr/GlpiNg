using GlpiNg.Modules.Abstractions.Localization;
namespace GlpiNg.Web.Components.Pages.AutomaticActions;

/// <summary>Formate une fréquence en minutes (voir AutomaticActionState.FrequencyMinutes) en libellé lisible, partagé entre Index et Detail.</summary>
internal static class FrequencyFormat
{
    public static string Label(int minutes) => minutes switch
    {
        < 60 => Tr.T("{0} min", minutes),
        _ when minutes % 1440 == 0 => $"{minutes / 1440} j",
        _ when minutes % 60 == 0 => $"{minutes / 60} h",
        _ => Tr.T("{0} min", minutes),
    };
}
