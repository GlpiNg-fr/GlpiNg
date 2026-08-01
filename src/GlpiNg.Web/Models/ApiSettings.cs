namespace GlpiNg.Web.Models;

/// <summary>Onglet "API".</summary>
public class ApiSettings
{
    public bool EnableHighLevelApi { get; set; }

    public bool EnableLegacyRestApi { get; set; } = true;

    public string LegacyApiUrl { get; set; } = "http://localhost/api.php/v1";

    public bool EnableLoginWithCredentials { get; set; } = true;

    public bool EnableLoginWithExternalToken { get; set; } = true;
}
