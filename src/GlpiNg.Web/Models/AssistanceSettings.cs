namespace GlpiNg.Web.Models;

/// <summary>Onglet "Assistance" : réglages ITIL (tickets), planning et matrice de priorité.</summary>
public class AssistanceSettings
{
    public int TimeStepMinutes { get; set; } = 5;

    /// <summary>Heure de début du planning, format "HH:mm".</summary>
    public string PlanningBegin { get; set; } = "08:00";

    /// <summary>Heure de fin du planning, format "HH:mm".</summary>
    public string PlanningEnd { get; set; } = "20:00";

    /// <summary>Jours ouvrés, 0 = dimanche ... 6 = samedi.</summary>
    public List<int> PlanningWorkDays { get; set; } = [1, 2, 3, 4, 5];

    /// <summary>Taille max. des pièces jointes acceptées par le collecteur de mails, en octets (0 = pas d'import).</summary>
    public long MailCollectorMaxFileSize { get; set; } = 2_097_152;

    public bool SoftwareLinkableToTicketByDefault { get; set; }

    public bool KeepTicketsOnAssetPurge { get; set; } = true;

    public bool ShowPersonalInfoOnSimplifiedTicketForm { get; set; } = true;

    public bool AllowAnonymousTicketCreation { get; set; }

    public bool AllowAnonymousFollowups { get; set; }

    /// <summary>Matrice priorité[urgence][impact] = priorité (1 = très basse ... 5 = très haute).</summary>
    public Dictionary<string, int> PriorityMatrix { get; set; } = BuildDefaultMatrix();

    /// <summary>Niveaux d'urgence/impact actifs (1,2,4,5 ; 3 = "moyenne" toujours actif).</summary>
    public List<int> EnabledUrgencyLevels { get; set; } = [1, 2, 3, 4, 5];

    public List<int> EnabledImpactLevels { get; set; } = [1, 2, 3, 4, 5];

    private static Dictionary<string, int> BuildDefaultMatrix()
    {
        // Valeurs par défaut GLPI : priorité = round((urgence + impact) / 2), plafonnée à 5.
        var matrix = new Dictionary<string, int>();
        for (var urgency = 1; urgency <= 5; urgency++)
        {
            for (var impact = 1; impact <= 5; impact++)
            {
                matrix[$"{urgency}_{impact}"] = Math.Min(5, (int)Math.Round((urgency + impact) / 2.0));
            }
        }

        return matrix;
    }
}
