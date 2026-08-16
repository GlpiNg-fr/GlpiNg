namespace GlpiNg.Web.Models;

/// <summary>
/// Ligne unique par section de réglages (ex. "ParcSettings", "GeneralSettings"), valeur
/// sérialisée en JSON. Remplace le stockage par section de appsettings.json, lu/écrit et
/// mis en cache mémoire par <see cref="Services.SettingsCacheService"/>. Voir
/// <see cref="ServerSettings"/> pour les réglages qui restent dans appsettings.json
/// (adresses d'écoute Kestrel, activation de Swagger).
/// </summary>
public class AppSetting
{
    public string SectionName { get; set; } = string.Empty;

    public string ValueJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
