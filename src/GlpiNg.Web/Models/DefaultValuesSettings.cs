namespace GlpiNg.Web.Models;

/// <summary>Onglet "Valeurs par défaut" : préférences appliquées par défaut aux nouveaux comptes.</summary>
public class DefaultValuesSettings
{
    public string Language { get; set; } = "fr_FR";

    /// <summary>ymd, dmy ou mdy.</summary>
    public string DateFormat { get; set; } = "ymd";

    /// <summary>NomPrenom ou PrenomNom.</summary>
    public string FullNameOrder { get; set; } = "NomPrenom";

    /// <summary>Code de format numérique, ex. "1 234.56".</summary>
    public string NumberFormat { get; set; } = "1 234.56";

    public int ResultsPerPage { get; set; } = 20;

    public bool GoToCreatedItem { get; set; }

    public bool ShowFullNameInDropdowns { get; set; }

    public bool ShowFullNameInSearchResults { get; set; } = true;

    /// <summary>0 = Non, 1 = Oui, 2 = Jamais.</summary>
    public int ShowCounters { get; set; } = 1;

    public bool ShowGlpiIds { get; set; }

    public bool KeepDevicesOnPurge { get; set; }

    public bool NotifyOnMyChanges { get; set; } = true;

    public int HomepageResultsCount { get; set; } = 5;

    public string PdfExportFont { get; set; } = "DejaVuSans";

    public string CsvDelimiter { get; set; } = ";";

    public string ColorPalette { get; set; } = "Auror";

    /// <summary>horizontal ou vertical.</summary>
    public string Layout { get; set; } = "vertical";

    /// <summary>embedded ou classic.</summary>
    public string RichTextLayout { get; set; } = "embedded";

    public bool HighContrast { get; set; }

    public string Timezone { get; set; } = "server";

    public string DefaultCentralTab { get; set; } = "dashboard";

    /// <summary>natural ou reverse.</summary>
    public string HistoryOrder { get; set; } = "natural";
}
