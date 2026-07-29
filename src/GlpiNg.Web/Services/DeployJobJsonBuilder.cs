using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Web.Models.Agent;

namespace GlpiNg.Web.Services;

/// <summary>
/// Construit la réponse JSON de la tâche "deploy" envoyée à GLPI-Agent.
///
/// Format vérifié par recoupement d'échanges réels agent/serveur (retours de la
/// communauté GLPI-Project) :
/// <code>
/// {
///   "jobs": {
///     "uuid": "...",
///     "checks": [ { "name": "...", "type": "fileExists", "path": "...", "value": "...", "return": "info" } ],
///     "associatedFiles": [ "&lt;sha512&gt;", ... ],
///     "actions": [ { "move": { "from": "...", "to": "...", "name": "..." } }, ... ],
///     "userinteractions": []
///   },
///   "associatedFiles": {
///     "&lt;sha512&gt;": { "name": "...", "p2p": "0", "p2p-retention-duration": "0" }
///   }
/// }
/// </code>
///
/// NB : le champ "uuid" utilisé ici sert de corrélation interne (identifiant du
/// <see cref="DeploymentJob"/>) — le protocole réel du plugin GlpiInventory utilise un
/// endpoint séparé à base de query-string (action=getJobs/setStatus) que ce serveur
/// choisit de ne pas reproduire tel quel : ce point est une adaptation, pas une donnée
/// vérifiée du protocole d'origine.
/// </summary>
public class DeployJobJsonBuilder
{
    public JsonObject Build(DeploymentJob job, DeploymentPackage package, string jobUuid)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(jobUuid);

        JsonArray actions = ParseActions(package.ActionsJson);

        JsonArray associatedFileHashes = [];
        JsonObject associatedFilesDetails = [];

        foreach (DeploymentPackageFile file in package.Files)
        {
            associatedFileHashes.Add(JsonValue.Create(file.Sha512));
            associatedFilesDetails[file.Sha512] = new JsonObject
            {
                ["name"] = file.FileName,
                ["p2p"] = "0",
                ["p2p-retention-duration"] = "0"
            };
        }

        JsonObject jobs = new()
        {
            ["uuid"] = jobUuid,
            ["checks"] = new JsonArray(),
            ["associatedFiles"] = associatedFileHashes,
            ["actions"] = actions,
            ["userinteractions"] = new JsonArray()
        };

        return new JsonObject
        {
            ["jobs"] = jobs,
            ["associatedFiles"] = associatedFilesDetails
        };
    }

    private static JsonArray ParseActions(string actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
        {
            return [];
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(actionsJson);
        }
        catch (JsonException)
        {
            return [];
        }

        if (parsed is JsonArray parsedArray)
        {
            return parsedArray;
        }

        return [];
    }
}
