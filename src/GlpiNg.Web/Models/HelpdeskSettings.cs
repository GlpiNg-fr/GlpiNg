namespace GlpiNg.Web.Models;

/// <summary>Onglet "Helpdesk" : surcharges de textes du portail libre-service par langue.</summary>
public class HelpdeskSettings
{
    public List<HelpdeskTranslation> Translations { get; set; } = [];
}

public class HelpdeskTranslation
{
    public string LanguageCode { get; set; } = "fr_FR";

    public string LanguageLabel { get; set; } = "Français";

    public string? WelcomeTitle { get; set; }

    public string? WelcomeMessage { get; set; }

    public string? SubmitButtonLabel { get; set; }
}
