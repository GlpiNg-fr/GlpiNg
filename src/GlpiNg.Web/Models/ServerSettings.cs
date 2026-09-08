namespace GlpiNg.Web.Models;

/// <summary>
/// Modèle de la page de configuration (/config). Reflète des clés de
/// appsettings.json que <see cref="Services.AppSettingsFileStore"/> lit et écrit.
/// </summary>
public class ServerSettings
{
    /// <summary>
    /// Adresses d'écoute du serveur Kestrel (clé de configuration "Urls"), ex.
    /// "http://0.0.0.0:5000". Un redémarrage de l'application est nécessaire pour
    /// qu'un changement soit pris en compte.
    /// </summary>
    public required string Urls { get; set; }

    /// <summary>
    /// Active ou désactive l'UI et le document OpenAPI Swagger (/swagger). Pris en
    /// compte immédiatement, sans redémarrage.
    /// </summary>
    public bool SwaggerEnabled { get; set; }

    /// <summary>
    /// Racine du stockage des fichiers de GlpiNg (clé "Storage:RootPath") : paquets de
    /// déploiement, clés de chiffrement. Vide = dossier <c>data</c> à la racine du programme.
    /// Comme <see cref="Urls"/>, la valeur est lue au démarrage et un redémarrage est nécessaire.
    /// </summary>
    public string? StorageRootPath { get; set; }
}
