using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Services;
using GlpiNg.Web.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Endpoint compatible protocole JSON GLPI-Agent.
/// Références utilisées pour l'implémentation :
///  - COMMON:   https://glpi-json-protocol.readthedocs.io/en/latest/common.html
///  - CONTACT:  https://glpi-json-protocol.readthedocs.io/en/latest/contact.html
///  - INVENTORY: https://glpi-json-protocol.readthedocs.io/en/latest/inventory.html
///
/// Traite aussi "getJobs" (récupération d'une tâche de déploiement) et "setStatus"
/// (rapport d'exécution), et expose le téléchargement des fichiers de package associés.
///
/// Ainsi que "getCollectJobs" / "setCollectAnswer" pour les collectes (registre, WMI, recherche
/// de fichiers) : même adaptation que le déploiement — une action sur la route unique plutôt que
/// le point d'accès b/collect du plugin d'origine.
///
/// Non couvert pour l'instant : compression brotli, chiffrement (GLPI-CryptoKey-ID),
/// proxy agent (GLPI-Proxy-ID). La compression zlib/gzip (Content-Type
/// "application/x-compress-zlib"/"-gzip") est décompressée en entrée. Le PROLOG legacy XML
/// (probe FusionInventory/OCS envoyé avant bascule sur le protocole JSON natif) est accepté
/// en entrée mais toujours répondu en JSON.
/// </summary>
[ApiController]
[Route("inventory")]
[Produces("application/json")]
[AllowAnonymous]
public class AgentController(
    GlpiNgDbContext db,
    InventoryImportService inventoryImport,
    DeployJobJsonBuilder deployJobJsonBuilder,
    NetworkJobJsonBuilder networkJobJsonBuilder,
    WakeOnLanJobJsonBuilder wakeOnLanJobJsonBuilder,
    NetworkDeviceImportService networkDeviceImport,
    DeploymentPackageFileStorageService fileStorage,
    SettingsCacheService settingsStore,
    NotificationDispatchService notificationDispatch,
    EventLogService eventLog,
    ILogger<AgentController> logger) : ControllerBase
{
    private const string AgentIdHeader = "GLPI-Agent-ID";
    private const string RequestIdHeader = "GLPI-Request-ID";

    /// <summary>
    /// L'agent GLPI envoie un GET ?action=getConfig en tout premier contact pour découvrir
    /// les capacités du serveur et obtenir l'URL de soumission d'inventaire. Sans réponse JSON
    /// valide ici, l'agent ne passera jamais au protocole POST (contact/inventory) et aucun
    /// ordinateur ne sera créé.
    ///
    /// Le thread de tâche Deploy de l'agent (GLPI::Agent::Task::Deploy::run, indépendant du cycle
    /// contact/inventory) envoie lui aussi un GET ?action=getConfig&amp;task[Deploy]=&lt;version&gt;
    /// juste avant de vérifier s'il y a un job à traiter, et n'appellera JAMAIS "getJobs" si le
    /// tableau "schedule" de cette réponse ne contient pas une entrée dont "task" vaut exactement
    /// "Deploy" (majuscule — comparaison stricte côté agent, voir Task/Deploy.pm : recherche
    /// "No Deploy job found in server jobs list." dans le module) — même si un DeploymentJob est
    /// bien en attente et déjà annoncé dans la réponse "contact" (POST). Sans cette entrée, un
    /// agent réel n'ira jamais chercher ses jobs de déploiement, quel que soit leur nombre en
    /// base — bug constaté en conditions réelles (agent GLPI-Agent 1.18) : "contact" annonce
    /// correctement le job en attente et réveille bien la tâche Deploy de l'agent, qui abandonne
    /// aussitôt après ce planning "Deploy" absent.
    ///
    /// Une fois "Deploy" annoncé, cette même tâche de l'agent envoie aussi "getJobs" et
    /// "setStatus" en GET avec des paramètres de requête plutôt qu'en POST avec un corps JSON —
    /// contrairement à contact/inventory. C'est GLPI::Agent::HTTP::Client::Fusion (protocole
    /// "Fusion", voir sa doc : "JSON messages sent through GET requests") qui porte ces trois
    /// actions pour la tâche Deploy, à la différence du client HTTP utilisé pour contact/inventory
    /// — constaté en conditions réelles : un vrai agent envoyant getJobs en GET recevait
    /// jusqu'ici un 400 "unsupported GET action 'getJobs'" de ce contrôleur, qui ne traitait ces
    /// deux actions que côté POST (voir <see cref="Handle"/>).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> HandleGet(CancellationToken cancellationToken)
    {
        string? action = Request.Query["action"];

        if (string.Equals(action, "getConfig", StringComparison.OrdinalIgnoreCase))
        {
            // La tâche Collect passe par le client « Fusion », qui ne pose pas l'en-tête
            // GLPI-Agent-ID : c'est le seul appel getConfig où l'agent peut donc être reconnu ici
            // (par son machineid, qui vaut son deviceid), les autres tâches ne s'identifiant qu'au
            // contact/getJobs suivant.
            string? machineId = Request.Query["machineid"];
            GlpiAgent? collectAgent = string.IsNullOrEmpty(machineId)
                ? null
                : await db.Agents.FirstOrDefaultAsync(a => a.DeviceId == machineId || a.AgentUuid == machineId, cancellationToken);

            if (collectAgent is not null)
            {
                UpdateAgentRequestMetadata(collectAgent);
                await db.SaveChangesAsync(cancellationToken);
            }

            string expiration = await GetExpirationAsync(cancellationToken);
            string serverUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/inventory";

            return Ok(new JsonObject
            {
                ["status"] = "ok",
                ["expiration"] = expiration,
                ["schedule"] = new JsonArray
                {
                    new JsonObject { ["task"] = "inventory", ["remote"] = serverUrl },
                    new JsonObject { ["task"] = "Deploy", ["remote"] = serverUrl },
                    // Même mécanisme de gate que "Deploy" ci-dessus (voir le commentaire de
                    // HandleGet) appliqué aux tâches NetDiscovery/NetInventory de GLPI-Agent —
                    // adaptation non vérifiée en conditions réelles, voir le commentaire de classe
                    // d'AgentController sur netdiscovery/netinventory.
                    new JsonObject { ["task"] = "NetDiscovery", ["remote"] = serverUrl },
                    new JsonObject { ["task"] = "NetInventory", ["remote"] = serverUrl },
                    // Même mécanisme de gate, adaptation non vérifiée en conditions réelles, pour
                    // la tâche WakeOnLan de GLPI-Agent — voir la doc de WakeOnLanTask.
                    new JsonObject { ["task"] = "WakeOnLan", ["remote"] = serverUrl },
                    // La tâche Collect ne lit pas ses travaux sur la route unique : elle demande
                    // cette configuration, y lit ce schedule, puis n'interroge plus que l'URL
                    // "remote" qu'il porte — voir CollectEndpoint.
                    new JsonObject
                    {
                        ["task"] = "Collect",
                        ["remote"] = $"{Request.Scheme}://{Request.Host}{CollectRemotePath}",
                        ["periodicity"] = 3600,
                    },
                }
            });
        }

        if (string.Equals(action, "getJobs", StringComparison.OrdinalIgnoreCase))
        {
            string? machineId = Request.Query["machineid"];
            GlpiAgent? agent = string.IsNullOrEmpty(machineId)
                ? null
                : await db.Agents.FirstOrDefaultAsync(a => a.DeviceId == machineId, cancellationToken);

            return await HandleGetJobsCoreAsync(agent, cancellationToken);
        }

        if (string.Equals(action, "getNetDiscoveryJobs", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "getNetInventoryJobs", StringComparison.OrdinalIgnoreCase))
        {
            string? machineId = Request.Query["machineid"];
            GlpiAgent? agent = string.IsNullOrEmpty(machineId)
                ? null
                : await db.Agents.FirstOrDefaultAsync(a => a.DeviceId == machineId, cancellationToken);

            NetworkTaskMethod method = string.Equals(action, "getNetDiscoveryJobs", StringComparison.OrdinalIgnoreCase)
                ? NetworkTaskMethod.NetworkDiscovery
                : NetworkTaskMethod.NetworkInventory;

            return await HandleGetNetworkJobsCoreAsync(agent, method, cancellationToken);
        }

        if (string.Equals(action, "getWakeOnLanJobs", StringComparison.OrdinalIgnoreCase))
        {
            string? machineId = Request.Query["machineid"];
            GlpiAgent? agent = string.IsNullOrEmpty(machineId)
                ? null
                : await db.Agents.FirstOrDefaultAsync(a => a.DeviceId == machineId, cancellationToken);

            return await HandleGetWakeOnLanJobsCoreAsync(agent, cancellationToken);
        }

        if (string.Equals(action, "setStatus", StringComparison.OrdinalIgnoreCase))
        {
            int? actionNum = int.TryParse(Request.Query["actionnum"], out int an) ? an : null;
            int? checkNum = int.TryParse(Request.Query["checknum"], out int cn) ? cn : null;

            SetStatusRequest statusRequest = new()
            {
                Uuid = Request.Query["uuid"],
                Part = Request.Query["part"],
                Sha512 = Request.Query["sha512"],
                Status = Request.Query["status"],
                ActionNum = actionNum,
                CheckNum = checkNum,
                Msg = Request.Query["msg"],
                CurrentStep = Request.Query["currentStep"]
            };

            return await HandleSetStatusCoreAsync(statusRequest, cancellationToken);
        }

        return BadRequest(new ProtocolAnswer { Status = "error", Message = $"unsupported GET action '{action}'" });
    }

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        // On lit le corps une seule fois (en octets, car il peut être compressé zlib/gzip),
        // pour pouvoir le logguer en cas d'erreur, puis on le reparse en JsonDocument pour
        // dispatcher sur "action".
        Request.EnableBuffering();
        using MemoryStream bodyBuffer = new();
        await Request.Body.CopyToAsync(bodyBuffer, cancellationToken);
        Request.Body.Position = 0;
        string rawBody = DecompressBody(bodyBuffer.ToArray(), Request.ContentType, Request.Headers.ContentEncoding);

        // L'agent GLPI envoie toujours un PROLOG XML historique (protocole FusionInventory/OCS)
        // en probe avant de savoir si le serveur supporte le protocole JSON natif. Un vrai
        // serveur GLPI répond à cette requête XML par un simple statut JSON, ce qui indique à
        // l'agent de basculer sur le protocole JSON (contact/inventory avec header
        // GLPI-Agent-ID) pour la suite. Sans cette réponse, l'agent boucle indéfiniment sur
        // PROLOG. Le header GLPI-Agent-ID n'est pas encore connu à ce stade, donc on répond
        // avant de le vérifier.
        string trimmedBody = rawBody.TrimStart();
        if (trimmedBody.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || trimmedBody.StartsWith("<REQUEST", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("inventory: PROLOG XML historique reçu, réponse JSON pour basculer l'agent sur le protocole natif.");
            return Ok(new ProtocolAnswer { Status = "ok", Expiration = await GetExpirationAsync(cancellationToken) });
        }

        // Le header GLPI-Agent-ID est l'identité réelle et stable de l'agent (protocole COMMON).
        if (!Request.Headers.TryGetValue(AgentIdHeader, out Microsoft.Extensions.Primitives.StringValues agentIdValues)
            || string.IsNullOrWhiteSpace(agentIdValues.ToString()))
        {
            logger.LogWarning("inventory 400: missing {Header} header. Headers reçus: {Headers}",
                AgentIdHeader, string.Join(", ", Request.Headers.Select(h => h.Key)));
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "missing GLPI-Agent-ID header" });
        }

        string agentUuid = agentIdValues.ToString();

        string action;
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawBody);
            action = document.RootElement.TryGetProperty("action", out JsonElement actionEl)
                ? actionEl.GetString() ?? "inventory"
                : "inventory"; // défaut du protocole COMMON quand "action" est absent
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "inventory 400: corps non-JSON (Content-Type={ContentType}, Content-Encoding={ContentEncoding}, {Length} octets). Début du corps: {Preview}",
                Request.ContentType, Request.Headers.ContentEncoding.ToString(), rawBody.Length,
                rawBody.Length > 200 ? rawBody[..200] : rawBody);
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "malformed json" });
        }

        using (document)
        {
            IActionResult result = action switch
            {
                "contact" => await HandleContactAsync(agentUuid, document, cancellationToken),
                "inventory" => await HandleInventoryAsync(agentUuid, document, cancellationToken),
                "getJobs" => await HandleGetJobsAsync(agentUuid, cancellationToken),
                "getNetDiscoveryJobs" => await HandleGetNetworkJobsAsync(agentUuid, NetworkTaskMethod.NetworkDiscovery, cancellationToken),
                "getNetInventoryJobs" => await HandleGetNetworkJobsAsync(agentUuid, NetworkTaskMethod.NetworkInventory, cancellationToken),
                "netdiscovery" or "netinventory" => await HandleNetworkInventoryAsync(agentUuid, action, document, cancellationToken),
                "getWakeOnLanJobs" => await HandleGetWakeOnLanJobsAsync(agentUuid, cancellationToken),
                "getCollectJobs" => await HandleGetCollectJobsAsync(agentUuid, cancellationToken),
                "setCollectAnswer" => await HandleSetCollectAnswerAsync(agentUuid, document, cancellationToken),
                "setStatus" => await HandleSetStatusAsync(document, cancellationToken),
                _ => BadRequest(new ProtocolAnswer { Status = "error", Message = $"unsupported action '{action}'" })
            };

            if (result is BadRequestObjectResult badRequest)
            {
                logger.LogWarning("inventory 400 sur action {Action}: {Answer}. Corps reçu: {Body}",
                    action, System.Text.Json.JsonSerializer.Serialize(badRequest.Value), rawBody);
            }

            // Le header GLPI-Request-ID, quand présent, doit être renvoyé tel quel (protocole COMMON).
            if (Request.Headers.TryGetValue(RequestIdHeader, out Microsoft.Extensions.Primitives.StringValues reqId))
            {
                Response.Headers[RequestIdHeader] = reqId;
            }
            Response.Headers[AgentIdHeader] = agentUuid;

            return result;
        }
    }

    /// <summary>
    /// Téléchargement d'un fichier de package référencé par son hash SHA512 (présent dans le
    /// tableau "associatedFiles" du job renvoyé par getJobs) : le fichier n'existe jamais comme
    /// blob unique sur disque (voir <see cref="DeploymentPackageFileStorageService"/>), ses
    /// fragments sont donc reconstitués à la volée dans l'ordre. Un agent capable d'exploiter le
    /// champ "multiparts" de associatedFiles devrait préférer <see cref="GetDeployFilePart"/>,
    /// fragment par fragment (plus léger, reprise possible sur échec réseau).
    /// </summary>
    [HttpGet("deploy/file/{sha512}")]
    public async Task<IActionResult> GetDeployFile(string sha512, CancellationToken cancellationToken)
    {
        DeploymentPackageFile? file = await db.DeploymentPackageFiles
            .Include(f => f.Parts)
            .FirstOrDefaultAsync(f => f.Sha512 == sha512, cancellationToken);

        if (file is null)
        {
            return NotFound();
        }

        List<string> orderedPartPaths = file.Parts.OrderBy(p => p.PartIndex).Select(p => p.StoragePath).ToList();
        if (orderedPartPaths.Count == 0 || orderedPartPaths.Exists(path => !System.IO.File.Exists(fileStorage.GetFullPath(path))))
        {
            return NotFound();
        }

        Response.ContentType = "application/octet-stream";
        Response.ContentLength = file.SizeBytes;
        Response.Headers.ContentDisposition = $"attachment; filename=\"{file.FileName}\"";

        await fileStorage.WriteReconstructedFileAsync(orderedPartPaths, Response.Body, cancellationToken);
        return new EmptyResult();
    }

    /// <summary>
    /// Téléchargement d'un fragment individuel d'un fichier de package (champ "multiparts" de
    /// associatedFiles dans le job renvoyé par getJobs) — voir <see cref="GetDeployFile"/> pour
    /// le téléchargement du fichier entier reconstitué. Un vrai agent n'appelle que la seconde
    /// route : il ajoute toujours <c>{c1}/{c1c2}/{sha512}</c> à l'URL du miroir
    /// (GLPI::Agent::Task::Deploy::File::_download) ; les deux segments de répartition ne servent
    /// qu'à sa convention d'URL, le fragment est retrouvé par son sha512 seul.
    /// </summary>
    [HttpGet("deploy/file/part/{sha512}")]
    [HttpGet("deploy/file/part/{shard1}/{shard2}/{sha512}")]
    public async Task<IActionResult> GetDeployFilePart(string sha512, CancellationToken cancellationToken)
    {
        DeploymentPackageFilePart? part = await db.DeploymentPackageFileParts
            .FirstOrDefaultAsync(p => p.Sha512 == sha512, cancellationToken);

        if (part is null)
        {
            return NotFound();
        }

        string fullPath = fileStorage.GetFullPath(part.StoragePath);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        FileStream stream = System.IO.File.OpenRead(fullPath);
        return File(stream, "application/octet-stream");
    }

    private async Task<IActionResult> HandleContactAsync(string agentUuid, JsonDocument document, CancellationToken cancellationToken)
    {
        ContactRequest? contactRequest;
        try
        {
            contactRequest = document.Deserialize<ContactRequest>(JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "inventory 400 (contact): échec de désérialisation. Corps: {Body}", document.RootElement.GetRawText());
            return BadRequest(new ProtocolAnswer { Status = "error", Message = $"malformed json: {ex.Message}" });
        }

        if (contactRequest is null)
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "malformed json" });
        }

        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        bool isNewAgent = agent is null;
        if (agent is null)
        {
            agent = new GlpiAgent { AgentUuid = agentUuid };
            db.Agents.Add(agent);
        }

        agent.DeviceId = contactRequest.DeviceId;
        // Le champ "name" du protocole COMMON vaut systématiquement "GLPI-Agent" côté
        // agent réel (nom du logiciel, pas du poste) : on utilise le deviceid, qui contient
        // le hostname, comme nom affiché par défaut.
        agent.AgentName = contactRequest.DeviceId;
        agent.AgentVersion = contactRequest.Version;
        agent.Tag = contactRequest.Tag;
        agent.InstalledTasks = contactRequest.InstalledTasks ?? [];
        agent.EnabledTasks = contactRequest.EnabledTasks ?? agent.InstalledTasks;
        agent.LastContactAt = DateTime.UtcNow;
        UpdateAgentRequestMetadata(agent);

        await db.SaveChangesAsync(cancellationToken);

        if (isNewAgent)
        {
            await notificationDispatch.PublishAsync(
                NotificationEventCatalog.DeploymentAgent,
                NotificationEventCatalog.EventNew,
                agent.Id,
                new Dictionary<string, string?>
                {
                    ["agent.name"] = agent.AgentName,
                    ["agent.uuid"] = agent.AgentUuid,
                    ["agent.version"] = agent.AgentVersion,
                    ["agent.url"] = $"{Request.Scheme}://{Request.Host}/tools/deployments/agent/{agent.Id}",
                },
                cancellationToken);

            await eventLog.LogAsync("inventory", EventLogLevel.Info, "Nouvel agent GLPI-Agent enregistré.",
                itemType: nameof(GlpiAgent), itemId: agent.Id, itemLabel: agent.AgentName ?? agent.AgentUuid,
                cancellationToken: cancellationToken);
        }

        List<DeployJobRef> pendingDeployJobs = await db.DeploymentJobs
            .Where(j => j.AgentId == agent.Id && j.Status == DeploymentStatus.Pending)
            .Select(j => new DeployJobRef { Task = "deploy", JobId = j.Id.ToString() })
            .ToListAsync(cancellationToken);

        // Même annonce que pendingDeployJobs ci-dessus pour les tâches réseau — uuid préfixé "net-"
        // (voir la doc de NetworkTaskJob) pour rester distinguable d'un DeploymentJob dans setStatus.
        List<NetworkTaskJob> pendingNetworkJobs = await db.NetworkTaskJobs
            .Include(j => j.Task)
            .Where(j => j.AgentId == agent.Id && j.Status == NetworkJobStatus.Pending)
            .ToListAsync(cancellationToken);

        List<DeployJobRef> pendingNetDiscoveryJobs = pendingNetworkJobs
            .Where(j => j.Task?.Method == NetworkTaskMethod.NetworkDiscovery)
            .Select(j => new DeployJobRef { Task = "netdiscovery", JobId = "net-" + j.Id.ToString("D8") })
            .ToList();

        List<DeployJobRef> pendingNetInventoryJobs = pendingNetworkJobs
            .Where(j => j.Task?.Method == NetworkTaskMethod.NetworkInventory)
            .Select(j => new DeployJobRef { Task = "netinventory", JobId = "net-" + j.Id.ToString("D8") })
            .ToList();

        // Même annonce que pendingDeployJobs/pendingNetworkJobs ci-dessus pour la tâche WakeOnLan —
        // uuid préfixé "wol-" (voir la doc de WakeOnLanTaskJob).
        List<DeployJobRef> pendingWakeOnLanJobs = await db.WakeOnLanTaskJobs
            .Where(j => j.AgentId == agent.Id && j.Status == WakeOnLanJobStatus.Pending)
            .Select(j => new DeployJobRef { Task = "wakeonlan", JobId = "wol-" + j.Id.ToString("D8") })
            .ToListAsync(cancellationToken);

        Dictionary<string, List<DeployJobRef>> jobsByTask = [];
        if (pendingDeployJobs.Count > 0) jobsByTask["deploy"] = pendingDeployJobs;
        if (pendingNetDiscoveryJobs.Count > 0) jobsByTask["netdiscovery"] = pendingNetDiscoveryJobs;
        if (pendingNetInventoryJobs.Count > 0) jobsByTask["netinventory"] = pendingNetInventoryJobs;
        if (pendingWakeOnLanJobs.Count > 0) jobsByTask["wakeonlan"] = pendingWakeOnLanJobs;

        var answer = new ContactAnswer
        {
            Status = "ok",
            Expiration = await GetExpirationAsync(cancellationToken),
            Jobs = jobsByTask.Count > 0 ? jobsByTask : null
        };

        return Ok(answer);
    }

    /// <summary>
    /// Message complet d'une exception, ses causes internes comprises.
    ///
    /// Une erreur d'enregistrement se réduit sinon à « An error occurred while saving the entity
    /// changes. See the inner exception for details. » — une phrase qui renvoie vers une exception
    /// que ni l'administrateur ni l'agent ne peuvent consulter. Ce sont les niveaux suivants qui
    /// nomment la table et la contrainte en cause.
    /// </summary>
    private static string Describe(Exception exception)
    {
        List<string> messages = [];

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (!messages.Contains(current.Message, StringComparer.Ordinal))
            {
                messages.Add(current.Message);
            }
        }

        return string.Join(" — ", messages);
    }

    /// <summary>
    /// Collectes à exécuter par l'agent : clés de registre, requêtes WMI, recherches de fichiers.
    ///
    /// Forme reprise de <c>PluginGlpiinventoryCollect::communication()</c> — un tableau
    /// <c>jobs</c> dont chaque entrée porte sa <c>function</c> (<c>getFromRegistry</c>,
    /// <c>getFromWMI</c>, <c>findFile</c>) et un <c>_sid</c> qui identifie l'entrée interrogée,
    /// renvoyé tel quel dans la réponse. Comme pour le déploiement, c'est une adaptation : GlpiNg
    /// expose une action sur sa route unique plutôt que le point d'accès <c>b/collect</c>.
    ///
    /// Ce sont toutes les collectes actives qui s'appliquent, sans ciblage par tâche. Le plugin
    /// d'origine passe par des tâches ; GlpiNg n'en a pas pour les collectes, et inventer un
    /// ciblage que rien ne configure serait pire que la règle simple et prévisible qu'on annonce.
    /// </summary>
    private async Task<IActionResult> HandleGetCollectJobsAsync(string agentUuid, CancellationToken cancellationToken)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);

        return await BuildCollectJobsAsync(agent, cancellationToken);
    }

    /// <summary>
    /// Travaux de collecte à exécuter, quelle que soit la route par laquelle ils sont demandés :
    /// l'action <c>getCollectJobs</c> de la route unique, ou le <c>getJobs</c> du protocole natif
    /// de la tâche Collect (voir <see cref="CollectEndpoint"/>).
    /// </summary>
    private async Task<IActionResult> BuildCollectJobsAsync(GlpiAgent? agent, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        UpdateAgentRequestMetadata(agent);
        await db.SaveChangesAsync(cancellationToken);

        List<CollectDefinition> collects = await db.CollectDefinitions
            .AsNoTracking()
            .Include(collect => collect.RegistryEntries)
            .Include(collect => collect.WmiEntries)
            .Include(collect => collect.FileSearchEntries)
            .Where(collect => collect.Enabled)
            .ToListAsync(cancellationToken);

        JsonArray jobs = [];

        foreach (CollectDefinition collect in collects)
        {
            foreach (CollectRegistryEntry entry in collect.RegistryEntries)
            {
                jobs.Add(new JsonObject
                {
                    ["function"] = "getFromRegistry",
                    ["uuid"] = Sid(collect.Id, "registry", entry.Id),
                    ["_sid"] = Sid(collect.Id, "registry", entry.Id),
                    ["_name"] = entry.Name,
                    ["path"] = RegistryPath(entry),
                });
            }

            foreach (CollectWmiEntry entry in collect.WmiEntries)
            {
                JsonArray properties = [];
                foreach (string property in (entry.Properties ?? string.Empty)
                             .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    properties.Add(property);
                }

                jobs.Add(new JsonObject
                {
                    ["function"] = "getFromWMI",
                    ["uuid"] = Sid(collect.Id, "wmi", entry.Id),
                    ["_sid"] = Sid(collect.Id, "wmi", entry.Id),
                    ["_name"] = entry.Name,
                    ["class"] = entry.WmiClass,
                    ["properties"] = properties,
                    ["moniker"] = entry.Moniker,
                });
            }

            foreach (CollectFileSearchEntry entry in collect.FileSearchEntries)
            {
                jobs.Add(new JsonObject
                {
                    ["function"] = "findFile",
                    ["uuid"] = Sid(collect.Id, "file", entry.Id),
                    ["_sid"] = Sid(collect.Id, "file", entry.Id),
                    ["_name"] = entry.Name,
                    ["dir"] = entry.Path,
                    ["filter"] = entry.Pattern,
                    ["recursive"] = entry.Recursive ? 1 : 0,
                });
            }
        }

        // GLPI::Agent::Task::Collect envoie "setAnswer" en GET, tous les champs en query string,
        // sauf si cette réponse porte "postmethod": "POST" — c'est alors seulement qu'il bascule
        // en POST application/x-www-form-urlencoded (voir Task/Collect.pm : "my $method =
        // exists($answer->{postmethod}) && $answer->{postmethod} eq 'POST' ? 'POST' : 'GET'").
        // Sans ce champ, un résultat de collecte arrivait en GET sur un point d'accès qui
        // n'acceptait que le corps POST attendu ici — 400 "setAnswer expects a form body" pour
        // chaque valeur remontée, constaté en conditions réelles (agent GLPI-Agent 1.18).
        //
        // "token" : ni le "action=setAnswer" en query string (en mode POST, seuls action/uuid/
        // method y figurent) ni le corps du formulaire (qui ne porte que uuid/_sid/_cpt et le
        // résultat lui-même) ne recontiennent jamais "machineid" — Collect.pm ne l'y met jamais
        // (voir "args => $result" dans _processRemote, qui ne construit jamais cette clé). Sans
        // moyen de retrouver l'agent, StoreCollectAnswersAsync tombait sur "agent est null" et
        // rendait silencieusement 200 sans rien écrire — constaté en conditions réelles : la
        // valeur remontait bien jusqu'à l'agent (log "Found REG_DWORD value: ..."), mais
        // CollectResults restait vide. Le champ "token" de la réponse est repris tel quel par
        // l'agent dans "_glpi_csrf_token" de chaque setAnswer suivant (Task/Collect.pm :
        // "$result->{_glpi_csrf_token} = $token if $token"), ce qui sert ici à transporter
        // l'identité de l'agent plutôt qu'un vrai jeton CSRF — adaptation, pas une reproduction
        // du protocole d'origine. Il doit aussi être réémis dans chaque réponse de setAnswer
        // (voir StoreCollectAnswersAsync), sous peine que l'agent traite son absence comme un
        // échec CSRF et abandonne les entrées suivantes du même job.
        return Ok(new JsonObject { ["jobs"] = jobs, ["postmethod"] = "POST", ["token"] = agent.AgentUuid });
    }

    /// <summary>
    /// Résultat d'une collecte, renvoyé par l'agent.
    ///
    /// Le <c>_sid</c> reçu est celui qui a été envoyé : il porte la collecte et l'entrée, ce qui
    /// évite d'avoir à faire correspondre des libellés dont rien ne garantit l'unicité.
    ///
    /// Un nouveau passage remplace le résultat précédent de la même entrée : une collecte dit
    /// l'état du poste à l'instant où elle a tourné, pas une accumulation.
    /// </summary>
    private async Task<IActionResult> HandleSetCollectAnswerAsync(string agentUuid, JsonDocument document, CancellationToken cancellationToken)
    {
        GlpiAgent? agent = await db.Agents
            .Include(a => a.Computer)
            .FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);

        return await StoreCollectAnswersAsync(agent, document.RootElement, agentUuid, cancellationToken);
    }

    /// <summary>
    /// Range les résultats d'une collecte, quelle que soit la route par laquelle ils arrivent :
    /// l'action <c>setCollectAnswer</c> de la route unique, ou le <c>setAnswer</c> du protocole
    /// natif de la tâche Collect (voir <see cref="CollectEndpoint"/>).
    /// </summary>
    private async Task<IActionResult> StoreCollectAnswersAsync(
        GlpiAgent? agent, JsonElement root, string agentLabel, CancellationToken cancellationToken)
    {
        if (agent?.Computer is not { } computer)
        {
            // Un agent sans ordinateur n'a pas encore envoyé d'inventaire : il n'y a nulle part où
            // ranger le résultat. Accepté sans rien écrire plutôt que rejeté, pour ne pas faire
            // boucler l'agent sur une erreur qu'il ne peut pas corriger.
            return Ok(new ProtocolAnswer { Status = "ok", Message = "no computer for this agent" });
        }

        int stored = 0;

        foreach (JsonElement answer in EnumerateAnswers(root))
        {
            // Le « _sid » n'est réémis par l'agent que s'il figurait dans le job ; l'« uuid »,
            // lui, l'est toujours (voir GLPI::Agent::Task::Collect). Les deux portent la même
            // valeur, et se replier sur l'uuid évite de perdre silencieusement un résultat.
            string? rawSid = ReadString(answer, "_sid") ?? ReadString(answer, "uuid");

            if (ParseSid(rawSid) is not { } sid)
            {
                logger.LogWarning("collect: réponse ignorée, identifiant d'entrée illisible ({Sid}).", rawSid ?? "absent");
                continue;
            }

            CollectEntryContext? entryContext = await ResolveCollectEntryContextAsync(sid, cancellationToken);

            // Le nom vient de la définition, pas de la réponse : c'est lui qui s'affiche sur la
            // fiche et qui sert de clé d'unicité, et rien ne garantit que l'agent réémette les
            // champs qu'il n'utilise pas. Le "_name" reçu n'est qu'un repli, et le libellé de la
            // nature un dernier recours pour ne jamais perdre une valeur remontée.
            string entryName = entryContext?.Name
                               ?? ReadString(answer, "_name")
                               ?? sid.Kind;

            CollectResult? existing = await db.CollectResults
                .FirstOrDefaultAsync(result => result.ComputerId == computer.Id
                                               && result.CollectDefinitionId == sid.CollectId
                                               && result.EntryName == entryName, cancellationToken);

            existing ??= AddResult(computer.Id, sid, entryName);

            (string? extractedKey, string? extractedValue) = ExtractCollectValue(sid.Kind, answer, entryContext);

            // Repli sur les noms de champs fixes puis sur le JSON brut seulement si rien de connu
            // n'a matché — pour ne jamais perdre silencieusement une valeur remontée sous une
            // forme imprévue.
            existing.Key = extractedKey ?? ReadString(answer, "key") ?? ReadString(answer, "path") ?? ReadString(answer, "class");
            existing.Value = extractedValue ?? ReadString(answer, "value") ?? ReadString(answer, "content") ?? RawValue(answer);
            existing.CollectedAt = DateTime.UtcNow;
            stored++;
        }

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("collect: agent {AgentUuid} — {Count} résultat(s) de collecte enregistré(s).", agentLabel, stored);

        // Réémis pour que l'agent le reprenne dans le prochain setAnswer du même job (plusieurs
        // entrées WMI/registre) — voir le commentaire de "token" dans BuildCollectJobsAsync.
        return Ok(new ProtocolAnswer { Status = "ok", Token = agent.AgentUuid });
    }

    // ---- Protocole natif de la tâche Collect ------------------------------------------------

    /// <summary>
    /// Chemin annoncé à l'agent comme « remote » de la tâche Collect. Route distincte de la route
    /// unique parce que la tâche y envoie <c>action=getJobs</c>, nom déjà pris par le déploiement :
    /// le plugin d'origine les distingue par leurs points d'accès, pas par l'action.
    /// </summary>
    private const string CollectRemotePath = "/inventory/collect";

    /// <summary>
    /// <c>GET|POST /inventory/collect</c> — travaux et résultats de la tâche Collect.
    ///
    /// Les deux verbes, parce que le client « Fusion » envoie <c>getJobs</c> en GET (paramètres en
    /// query string) et <c>setAnswer</c> en POST <c>application/x-www-form-urlencoded</c>, avec
    /// l'action dans l'URL et les valeurs dans le corps. Ni l'un ni l'autre n'est du JSON, ce que
    /// la route unique attend pourtant : c'est pourquoi ils ne pouvaient pas y aboutir.
    ///
    /// Un résultat arrive seul, à plat, une requête par valeur remontée.
    /// </summary>
    [HttpGet("collect")]
    [HttpPost("collect")]
    public async Task<IActionResult> CollectEndpoint(CancellationToken cancellationToken)
    {
        string? action = Request.Query["action"].FirstOrDefault();
        string? machineId = Request.Query["machineid"].FirstOrDefault();

        // Le corps d'un setAnswer porte les valeurs collectées, et peut aussi porter l'action et
        // l'identité quand le client ne les a pas mises dans l'URL.
        IFormCollection? form = Request.HasFormContentType
            ? await Request.ReadFormAsync(cancellationToken)
            : null;

        if (form is not null)
        {
            action ??= form["action"].FirstOrDefault();
            machineId ??= form["machineid"].FirstOrDefault();
        }

        GlpiAgent? agent = await FindAgentByMachineIdAsync(machineId, cancellationToken);

        if (string.Equals(action, "getJobs", StringComparison.OrdinalIgnoreCase))
        {
            return await BuildCollectJobsAsync(agent, cancellationToken);
        }

        if (string.Equals(action, "setAnswer", StringComparison.OrdinalIgnoreCase))
        {
            if (form is null)
            {
                return BadRequest(new ProtocolAnswer { Status = "error", Message = "setAnswer expects a form body" });
            }

            // setAnswer ne porte jamais de machineid (Collect.pm ne le met que dans les args de
            // getJobs) : on retombe sur "_glpi_csrf_token", où getJobs avait placé l'AgentUuid —
            // voir le commentaire de "token" dans BuildCollectJobsAsync.
            agent ??= await ResolveAgentByCollectTokenAsync(form["_glpi_csrf_token"].FirstOrDefault(), cancellationToken);

            // Le formulaire est converti en objet JSON pour être rangé par le même code que
            // l'action setCollectAnswer : ce qui diffère est le transport, pas le résultat.
            JsonObject answer = [];

            foreach (string key in form.Keys)
            {
                answer[key] = form[key].FirstOrDefault();
            }

            using JsonDocument document = JsonDocument.Parse(answer.ToJsonString());

            return await StoreCollectAnswersAsync(
                agent, document.RootElement, machineId ?? "(inconnu)", cancellationToken);
        }

        // Envoyé une fois par job en fin de tâche (GLPI::Agent::Task::Collect::run). Le plugin
        // d'origine y clôt l'état du job ; ici les collectes sont sans état (toutes les
        // collectes actives, à chaque passage), il n'y a donc rien à clore. Répondu plutôt que
        // rejeté : un 400 laissait une erreur « unsupported action 'jobsDone' » dans le journal de
        // l'agent à chaque exécution. L'agent ne vérifie que la présence d'une réponse.
        if (string.Equals(action, "jobsDone", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new JsonObject());
        }

        return BadRequest(new ProtocolAnswer { Status = "error", Message = $"unsupported action '{action}'" });
    }

    /// <summary>
    /// Retrouve l'agent par le <c>machineid</c> de la tâche Collect, qui vaut son
    /// <c>deviceid</c> d'inventaire — le seul identifiant dont elle dispose, le client qu'elle
    /// utilise ne posant pas d'en-tête <c>GLPI-Agent-ID</c>.
    /// </summary>
    private async Task<GlpiAgent?> FindAgentByMachineIdAsync(string? machineId, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(machineId)
            ? null
            : await db.Agents
                .Include(a => a.Computer)
                .FirstOrDefaultAsync(a => a.DeviceId == machineId || a.AgentUuid == machineId, cancellationToken);

    /// <summary>Retrouve l'agent pour un « setAnswer » de la tâche Collect à partir du jeton
    /// renvoyé tel quel par l'agent — voir le commentaire de « token » dans
    /// <see cref="BuildCollectJobsAsync"/>.</summary>
    private async Task<GlpiAgent?> ResolveAgentByCollectTokenAsync(string? token, CancellationToken cancellationToken)
        => string.IsNullOrWhiteSpace(token)
            ? null
            : await db.Agents
                .Include(a => a.Computer)
                .FirstOrDefaultAsync(a => a.AgentUuid == token, cancellationToken);

    /// <summary>Ce qu'il faut de la définition d'une entrée de collecte pour interpréter sa
    /// réponse : son nom d'affichage, et de quoi retrouver la donnée utile dans le JSON reçu
    /// (voir <see cref="ExtractCollectValue"/>).</summary>
    private sealed record CollectEntryContext(string? Name, string? RegistryKey, string? WmiProperties);

    /// <summary>Contexte de l'entrée de collecte désignée par le <c>_sid</c>, ou null si elle a
    /// été supprimée depuis l'envoi du job.</summary>
    private async Task<CollectEntryContext?> ResolveCollectEntryContextAsync(
        (int CollectId, string Kind, int EntryId) sid, CancellationToken cancellationToken) =>
        sid.Kind switch
        {
            "wmi" => await db.Set<CollectWmiEntry>()
                .Where(entry => entry.Id == sid.EntryId && entry.CollectDefinitionId == sid.CollectId)
                .Select(entry => new CollectEntryContext(entry.Name, null, entry.Properties))
                .FirstOrDefaultAsync(cancellationToken),
            "file" => await db.Set<CollectFileSearchEntry>()
                .Where(entry => entry.Id == sid.EntryId && entry.CollectDefinitionId == sid.CollectId)
                .Select(entry => new CollectEntryContext(entry.Name, null, null))
                .FirstOrDefaultAsync(cancellationToken),
            _ => await db.Set<CollectRegistryEntry>()
                .Where(entry => entry.Id == sid.EntryId && entry.CollectDefinitionId == sid.CollectId)
                .Select(entry => new CollectEntryContext(entry.Name, entry.RegistryKey, null))
                .FirstOrDefaultAsync(cancellationToken),
        };

    /// <summary>
    /// La donnée collectée n'arrive jamais sous un champ fixe ("value"/"content") : elle est
    /// nommée d'après ce qui a été demandé, exactement comme le fait GLPI-Agent lui-même —
    /// constaté en conditions réelles, une réponse de registre ne portait que la paire
    /// <c>{"UEFISecureBootEnabled": "0x00000001"}</c> au milieu des champs de protocole
    /// (action/uuid/_sid/_cpt/_glpi_csrf_token), jamais de "value".
    ///
    /// - registre (<c>GLPI::Agent::Task::Collect::Registry::results</c>) : une seule clé
    ///   dynamique, le dernier segment du chemin interrogé — c'est <c>RegistryKey</c> côté
    ///   définition.
    /// - WMI (<c>...::WMI::results</c>) : un objet par instance WMI, une clé par propriété
    ///   demandée (<c>Properties</c>, liste séparée par virgules) — peut donc porter plusieurs
    ///   valeurs à la fois, ce qu'un seul couple Clé/Valeur ne représente qu'en les concaténant.
    /// - recherche de fichiers (<c>...::File::results</c>) : toujours les deux champs fixes
    ///   <c>path</c>/<c>size</c>, eux bien nommés d'origine.
    /// </summary>
    private (string? Key, string? Value) ExtractCollectValue(string kind, JsonElement answer, CollectEntryContext? entry)
    {
        switch (kind)
        {
            case "wmi":
                List<string> names = [.. (entry?.WmiProperties ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(name => ReadString(answer, name) is not null)];

                return names.Count switch
                {
                    0 => (null, null),
                    1 => (names[0], ReadString(answer, names[0])),
                    _ => (string.Join(", ", names),
                          string.Join("; ", names.Select(name => $"{name}={ReadString(answer, name)}"))),
                };

            case "file":
                return (ReadString(answer, "path"), ReadString(answer, "size"));

            default: // "registry"
                return entry?.RegistryKey is { } registryKey
                    ? (registryKey, ReadString(answer, registryKey))
                    : (null, null);
        }
    }

    private CollectResult AddResult(int computerId, (int CollectId, string Kind, int EntryId) sid, string entryName)
    {
        CollectResult created = new()
        {
            ComputerId = computerId,
            CollectDefinitionId = sid.CollectId,
            Type = sid.Kind switch
            {
                "wmi" => CollectType.Wmi,
                "file" => CollectType.FileSearch,
                _ => CollectType.Registry,
            },
            EntryName = entryName,
        };

        db.CollectResults.Add(created);
        return created;
    }

    /// <summary>Les réponses arrivent soit en tableau, soit une par requête : les deux sont acceptées.</summary>
    private static IEnumerable<JsonElement> EnumerateAnswers(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray();
        }

        if (root.TryGetProperty("answers", out JsonElement answers) && answers.ValueKind == JsonValueKind.Array)
        {
            return answers.EnumerateArray();
        }

        return [root];
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind is not JsonValueKind.Null
            ? value.ToString()
            : null;

    /// <summary>Repli quand l'agent rend la valeur sans nom de champ connu : on garde le corps tel
    /// quel plutôt que d'enregistrer un résultat vide.</summary>
    private static string? RawValue(JsonElement element) =>
        element.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? element.GetRawText() : element.ToString();

    /// <summary>
    /// Ruche, chemin et clé recollés en un seul chemin, sans doubler les séparateurs que la saisie
    /// peut déjà porter aux extrémités.
    ///
    /// Contrairement à l'usage Win32 courant, le champ « path » de la tâche Collect n'accepte pas
    /// le séparateur « \ » : GLPI::Agent::Tools::Win32 configure son accès à Win32::TieRegistry
    /// avec <c>Delimiter => '/'</c>, et <c>getRegistryValue</c> découpe explicitement le chemin sur
    /// « / » (<c>m{^(HKEY_\w+.*)/([^/]+)/([^/]+)}</c>) pour en tirer la ruche, la clé et la valeur.
    /// Joindre les segments avec « \ » — comme le faisait la première version — laisse le régex
    /// découper au mauvais endroit dès que le chemin saisi contient lui-même des « / », et le
    /// résultat corrompu part droit dans <c>$Registry->Open()</c> : constaté en conditions réelles,
    /// un crash Perl (« Usage: Win32API::Registry::regConstant("CONST_NAME") ») là où l'agent
    /// attendait justement <c>HKEY_LOCAL_MACHINE/SYSTEM/.../SecureBoot/State/UEFISecureBootEnabled</c>.
    /// Toute saisie en « \ », plus naturelle pour un humain habitué au Registre, est donc convertie
    /// ici plutôt que renvoyée telle quelle.
    /// </summary>
    private static string RegistryPath(CollectRegistryEntry entry) =>
        string.Join('/', new[] { entry.Hive, entry.Path, entry.RegistryKey }
            .Select(part => part.Replace('\\', '/').Trim('/'))
            .Where(part => part.Length > 0));

    private static string Sid(int collectId, string kind, int entryId) => $"{collectId}:{kind}:{entryId}";

    private static (int CollectId, string Kind, int EntryId)? ParseSid(string? sid)
    {
        string[] parts = (sid ?? string.Empty).Split(':');

        return parts.Length == 3
               && int.TryParse(parts[0], out int collectId)
               && int.TryParse(parts[2], out int entryId)
            ? (collectId, parts[1], entryId)
            : null;
    }

    private async Task<IActionResult> HandleInventoryAsync(string agentUuid, JsonDocument document, CancellationToken cancellationToken)
    {
        InventoryRequest? inventoryRequest;
        try
        {
            inventoryRequest = document.Deserialize<InventoryRequest>(JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "inventory 400 (inventory): échec de désérialisation. Clés reçues à la racine: {Keys}",
                string.Join(", ", document.RootElement.EnumerateObject().Select(p => p.Name)));
            return BadRequest(new ProtocolAnswer { Status = "error", Message = $"bad-format: {ex.Message}" });
        }

        if (inventoryRequest is null)
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        // Réglage "Activer l'inventaire" de /admin/inventory : on répond quand même
        // "ok" (comme GLPI) pour ne pas déclencher de boucle de re-essai côté agent, mais sans
        // toucher aux ordinateurs.
        InventorySettings inventorySettings =
            await settingsStore.ReadSectionAsync<InventorySettings>("InventorySettings", cancellationToken);
        string expiration = FormatExpiration(inventorySettings.InventoryFrequencyHours);

        if (!inventorySettings.Enabled)
        {
            // Warning et non Information : de l'extérieur, un inventaire ignoré est indiscernable
            // d'un inventaire traité (l'agent reçoit "ok" dans les deux cas, pour ne pas boucler).
            // Sans trace visible dans les Journaux, un réglage laissé à "désactivé" se diagnostique
            // très mal — c'est exactement le symptôme "l'agent remonte mais aucun ordinateur
            // n'apparaît".
            logger.LogWarning("inventory: inventaire DÉSACTIVÉ dans /admin/inventory — requête ignorée pour l'agent {AgentUuid}, aucun ordinateur créé.", agentUuid);
            await eventLog.LogAsync("inventory", EventLogLevel.Warning,
                "Inventaire reçu mais ignoré : la prise en compte des inventaires est désactivée (Administration > Inventaire).",
                itemLabel: agentUuid, cancellationToken: cancellationToken);

            return Ok(new ProtocolAnswer { Status = "ok", Expiration = expiration });
        }

        // Le protocole agent est volontairement exempté du gate de migrations (voir
        // MigrationsGateMiddleware) pour qu'un parc ne cesse pas de remonter pendant qu'un
        // administrateur applique une mise à jour. La contrepartie est que l'agent écrit alors
        // contre un schéma qui ne correspond plus au modèle : l'échec se produit au fond d'EF, sur
        // une colonne inconnue, et se lit très mal. On le dit clairement ici.
        if (await db.Database.GetPendingMigrationsAsync(cancellationToken) is { } pending && pending.Any())
        {
            logger.LogError(
                "inventory: {Count} migration(s) en attente ({Migrations}) — inventaire de l'agent {AgentUuid} non traité. Appliquez-les depuis /update.",
                pending.Count(), string.Join(", ", pending), agentUuid);

            await eventLog.LogAsync("inventory", EventLogLevel.Error,
                $"Inventaire non traité : {pending.Count()} migration(s) de base de données en attente. Appliquez-les depuis /update.",
                itemLabel: agentUuid, cancellationToken: cancellationToken);

            return StatusCode(503, new ProtocolAnswer { Status = "error", Message = "server database upgrade pending" });
        }

        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        if (agent is null)
        {
            // Un agent peut en théorie envoyer un inventory sans contact préalable
            // (ex: injection manuelle) — on le crée à la volée plutôt que de rejeter l'inventaire.
            agent = new GlpiAgent { AgentUuid = agentUuid, DeviceId = inventoryRequest.DeviceId };
            db.Agents.Add(agent);
        }

        UpdateAgentRequestMetadata(agent);

        // Le contact de l'agent est enregistré avant l'import, et non plus par le SaveChanges de
        // celui-ci : les deux ne doivent pas être solidaires. Un import qui échoue faisait perdre
        // la trace du contact, et donnait l'illusion inverse — un agent à jour (par un "contact"
        // précédent) et un ordinateur jamais créé, sans rien pour relier les deux.
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            Computer? computer = await inventoryImport.ImportAsync(agent, inventoryRequest.Content, cancellationToken);

            if (computer is null)
            {
                // ImportAsync ne rend null que sur refus par une règle d'affectation à l'import.
                // Le refus est déjà tracé dans RefusedImportLog (/admin/import-rules/refused),
                // mais rien ne le signalait côté journal applicatif.
                logger.LogWarning("inventory: inventaire de l'agent {AgentUuid} REFUSÉ par une règle d'affectation à l'import — aucun ordinateur créé.", agentUuid);
                await eventLog.LogAsync("inventory", EventLogLevel.Warning,
                    "Inventaire refusé par une règle d'affectation à l'import : voir Administration > Règles d'import > Imports refusés.",
                    itemLabel: agent.DeviceId ?? agentUuid, cancellationToken: cancellationToken);
            }
            else
            {
                logger.LogInformation("inventory: agent {AgentUuid} — ordinateur « {Computer} » (#{ComputerId}) importé.",
                    agentUuid, computer.Name, computer.Id);
            }
        }
        catch (Exception ex)
        {
            // Sans ce bloc, l'échec remontait en 500 nu : l'agent réessayait indéfiniment et rien
            // n'indiquait quel agent ni quelle étape avait échoué. On trace, puis on répond une
            // erreur explicite — le contact de l'agent, lui, est déjà enregistré.
            // Journal applicatif (console/fichier) d'abord : il ne dépend pas de la base, donc il
            // reste lisible même quand c'est précisément la base qui pose problème. Le journal
            // consultable dans l'UI, lui, ne peut par construction rien dire d'une panne d'écriture.
            logger.LogError(ex, "inventory: échec de l'import de l'inventaire de l'agent {AgentUuid} ({Device}).",
                agentUuid, agent.DeviceId ?? agent.Hostname);

            try
            {
                await eventLog.LogAsync("inventory", EventLogLevel.Error,
                    $"Échec de l'import de l'inventaire : {Describe(ex)}",
                    itemLabel: agent.DeviceId ?? agentUuid, cancellationToken: cancellationToken);
            }
            catch (Exception logEx)
            {
                // Écrire la trace ne doit jamais masquer l'erreur d'origine par la sienne.
                logger.LogError(logEx, "inventory: échec de l'écriture au journal de l'erreur ci-dessus.");
            }

            return StatusCode(500, new ProtocolAnswer { Status = "error", Message = $"inventory import failed: {Describe(ex)}" });
        }

        return Ok(new ProtocolAnswer { Status = "ok", Expiration = expiration });
    }

    /// <summary>
    /// Traite les résultats "netdiscovery"/"netinventory" — voir <see cref="NetworkInventoryRequest"/>
    /// pour la forme attendue (auto-inventée, distincte du schéma "inventory" standard). Écrit
    /// uniquement les <see cref="DiscoveredNetworkDevice"/> trouvés (via
    /// <see cref="NetworkDeviceImportService"/>) ; la clôture du <see cref="NetworkTaskJob"/>
    /// correspondant reste signalée par un "setStatus" séparé (uuid préfixé "net-"), comme pour
    /// "deploy" — cohérence avec le protocole existant plutôt qu'un signal de fin ad hoc ici.
    ///
    /// Comme le job uuid n'est pas porté par ce résultat dans l'adaptation retenue ici (voir le
    /// commentaire de classe), on rattache les équipements trouvés au NetworkTaskJob "Running" le
    /// plus récent de cet agent pour la méthode concernée (best-effort — laisse
    /// DiscoveredNetworkDevice.DiscoveredViaNetworkTaskId nul si aucun job en cours ne correspond).
    /// </summary>
    private async Task<IActionResult> HandleNetworkInventoryAsync(string agentUuid, string action, JsonDocument document, CancellationToken cancellationToken)
    {
        NetworkInventoryRequest? request;
        try
        {
            request = document.Deserialize<NetworkInventoryRequest>(JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "inventory 400 ({Action}): échec de désérialisation. Corps: {Body}", action, document.RootElement.GetRawText());
            return BadRequest(new ProtocolAnswer { Status = "error", Message = $"bad-format: {ex.Message}" });
        }

        if (request is null)
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        NetworkTaskMethod method = string.Equals(action, "netdiscovery", StringComparison.OrdinalIgnoreCase)
            ? NetworkTaskMethod.NetworkDiscovery
            : NetworkTaskMethod.NetworkInventory;

        int? networkTaskId = null;
        if (agent is not null)
        {
            networkTaskId = await db.NetworkTaskJobs
                .Where(j => j.AgentId == agent.Id && j.Status == NetworkJobStatus.Running && j.Task != null && j.Task.Method == method)
                .OrderByDescending(j => j.StartedAt)
                .Select(j => (int?)j.NetworkTaskId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        int newDeviceCount = await networkDeviceImport.ImportAsync(networkTaskId, request.Content, cancellationToken);
        logger.LogInformation("inventory: {Action} de l'agent {AgentUuid} — {Count} équipement(s) découvert(s)/mis à jour, {NewCount} nouveau(x).",
            action, agentUuid, request.Content.Devices.Count, newDeviceCount);

        return Ok(new ProtocolAnswer { Status = "ok", Expiration = await GetExpirationAsync(cancellationToken) });
    }

    private async Task<IActionResult> HandleGetJobsAsync(string agentUuid, CancellationToken cancellationToken)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        return await HandleGetJobsCoreAsync(agent, cancellationToken);
    }

    /// <summary>Cœur commun aux deux façons dont un agent peut demander ses jobs de déploiement :
    /// "getJobs" en POST (identifié par l'en-tête GLPI-Agent-ID, voir <see cref="HandleGetJobsAsync"/>)
    /// et "getJobs" en GET (identifié par le paramètre "machineid", voir <see cref="HandleGet"/>).</summary>
    /// <remarks>Sans job, la réponse est un objet vide : c'est la seule forme que
    /// GLPI::Agent::Task::Deploy::processRemote lit comme « Nothing to do ». Un
    /// <c>{"jobs":[]}</c> passait par _validateAnswer, qui le rejetait (« bad JSON: missing
    /// associatedFiles key ») à chaque exécution de la tâche.</remarks>
    private async Task<IActionResult> HandleGetJobsCoreAsync(GlpiAgent? agent, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return Ok(new JsonObject());
        }

        DeploymentJob? job = await db.DeploymentJobs
            .Include(j => j.Package)
            .ThenInclude(p => p!.Files)
            .ThenInclude(f => f.Parts)
            .Where(j => j.AgentId == agent.Id && j.Status == DeploymentStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null || job.Package is null)
        {
            return Ok(new JsonObject());
        }

        string jobUuid = job.Id.ToString("D8");
        // L'agent complète lui-même ce miroir en {c1}/{c1c2}/{sha512} — voir GetDeployFilePart.
        string mirror = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/inventory/deploy/file/part/";
        JsonObject payload = deployJobJsonBuilder.Build(job, job.Package, jobUuid, [mirror]);

        job.Status = DeploymentStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Content(payload.ToJsonString(), "application/json");
    }

    private async Task<IActionResult> HandleGetNetworkJobsAsync(string agentUuid, NetworkTaskMethod method, CancellationToken cancellationToken)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        return await HandleGetNetworkJobsCoreAsync(agent, method, cancellationToken);
    }

    /// <summary>Cœur commun aux deux façons dont un agent peut demander ses jobs "netdiscovery"/
    /// "netinventory" — structurellement identique à <see cref="HandleGetJobsCoreAsync"/> pour
    /// "deploy". L'uuid renvoyé est préfixé "net-" (voir la doc de NetworkTaskJob) pour rester
    /// distinguable d'un DeploymentJob dans "setStatus" (voir <see cref="HandleSetStatusCoreAsync"/>).
    /// </summary>
    private async Task<IActionResult> HandleGetNetworkJobsCoreAsync(GlpiAgent? agent, NetworkTaskMethod method, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        NetworkTaskJob? job = await db.NetworkTaskJobs
            .Include(j => j.Task).ThenInclude(t => t!.IpRanges).ThenInclude(r => r.IpRange)
            .Include(j => j.Task).ThenInclude(t => t!.Credentials).ThenInclude(c => c.SnmpCredential)
            .Where(j => j.AgentId == agent.Id && j.Status == NetworkJobStatus.Pending && j.Task != null && j.Task.Method == method)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null || job.Task is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        string jobUuid = "net-" + job.Id.ToString("D8");
        JsonObject payload = networkJobJsonBuilder.Build(job, job.Task, jobUuid);

        job.Status = NetworkJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Content(payload.ToJsonString(), "application/json");
    }

    private async Task<IActionResult> HandleGetWakeOnLanJobsAsync(string agentUuid, CancellationToken cancellationToken)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        return await HandleGetWakeOnLanJobsCoreAsync(agent, cancellationToken);
    }

    /// <summary>Cœur commun aux deux façons dont un agent relais peut demander ses jobs
    /// "wakeonlan" — structurellement identique à <see cref="HandleGetNetworkJobsCoreAsync"/>, sauf
    /// que la spec (cibles/MAC) n'est pas lue en direct depuis la tâche mais depuis
    /// <see cref="WakeOnLanTaskJob.TargetMacsJson"/> (voir sa doc). L'uuid renvoyé est préfixé
    /// "wol-" pour rester distinguable d'un DeploymentJob/NetworkTaskJob dans "setStatus".</summary>
    private async Task<IActionResult> HandleGetWakeOnLanJobsCoreAsync(GlpiAgent? agent, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        WakeOnLanTaskJob? job = await db.WakeOnLanTaskJobs
            .Where(j => j.AgentId == agent.Id && j.Status == WakeOnLanJobStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (job is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        string jobUuid = "wol-" + job.Id.ToString("D8");
        JsonObject payload = wakeOnLanJobJsonBuilder.Build(job, jobUuid);

        job.Status = WakeOnLanJobStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Content(payload.ToJsonString(), "application/json");
    }

    /// <summary>
    /// Traite un rapport "setStatus" — l'agent en envoie un par étape (démarrage, chaque check,
    /// chaque fichier téléchargé, préparation, chaque ligne de log d'action, fin de job), pas un
    /// seul message final. Chaque appel est journalisé (affiché dans l'onglet "Journal" de
    /// /tools/deployments/supervision, voir Supervision.razor) ; le job n'est marqué
    /// Success/Error que sur un signal non ambigu :
    ///  - "status":"ko" à n'importe quelle étape → échec (l'agent arrête le job après un ko, il
    ///    n'enverra plus de message pour ce uuid, voir GLPI::Agent::Task::Deploy::processRemote) ;
    ///  - "status":"ok" niveau job (part absent/"job") sans "currentStep" → seul le tout dernier
    ///    message d'un job réussi n'a pas de currentStep (l'agent le vide avant de l'envoyer, voir
    ///    Job::currentStep('end')) ; tous les "ok" intermédiaires (checks, téléchargement,
    ///    préparation, chaque action) portent un currentStep renseigné et ne doivent donc pas
    ///    clore le job prématurément.
    /// </summary>
    private async Task<IActionResult> HandleSetStatusAsync(JsonDocument document, CancellationToken cancellationToken)
    {
        SetStatusRequest? statusRequest;
        try
        {
            statusRequest = document.Deserialize<SetStatusRequest>(JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "inventory 400 (setStatus): échec de désérialisation. Corps: {Body}", document.RootElement.GetRawText());
            return BadRequest(new ProtocolAnswer { Status = "error", Message = $"malformed json: {ex.Message}" });
        }

        if (statusRequest is null)
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        return await HandleSetStatusCoreAsync(statusRequest, cancellationToken);
    }

    /// <summary>Cœur commun aux deux façons dont un agent peut rapporter le statut d'un job :
    /// "setStatus" en POST avec un corps JSON (voir <see cref="HandleSetStatusAsync"/>) et
    /// "setStatus" en GET avec des paramètres de requête (voir <see cref="HandleGet"/>).</summary>
    private const string NetworkJobUuidPrefix = "net-";
    private const string WakeOnLanJobUuidPrefix = "wol-";

    private async Task<IActionResult> HandleSetStatusCoreAsync(SetStatusRequest statusRequest, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(statusRequest.Uuid))
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        // Les NetworkTaskJob sont exposés à l'agent sous un uuid préfixé "net-" (voir la doc de
        // NetworkTaskJob) pour rester distinguables des DeploymentJob ici : les deux sont des
        // entiers qui se chevauchent, "setStatus" reçoit le même uuid quel que soit le type de job.
        if (statusRequest.Uuid.StartsWith(NetworkJobUuidPrefix, StringComparison.Ordinal))
        {
            string rawId = statusRequest.Uuid[NetworkJobUuidPrefix.Length..];
            if (!int.TryParse(rawId, out int networkJobId))
            {
                return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
            }

            return await HandleSetNetworkJobStatusCoreAsync(networkJobId, statusRequest, cancellationToken);
        }

        // Même principe pour les WakeOnLanTaskJob, préfixés "wol-".
        if (statusRequest.Uuid.StartsWith(WakeOnLanJobUuidPrefix, StringComparison.Ordinal))
        {
            string rawId = statusRequest.Uuid[WakeOnLanJobUuidPrefix.Length..];
            if (!int.TryParse(rawId, out int wakeOnLanJobId))
            {
                return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
            }

            return await HandleSetWakeOnLanJobStatusCoreAsync(wakeOnLanJobId, statusRequest, cancellationToken);
        }

        if (!int.TryParse(statusRequest.Uuid, out int jobId))
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        DeploymentJob? job = await db.DeploymentJobs
            .Include(j => j.Package)
            .Include(j => j.Agent)
            .ThenInclude(a => a!.Computer)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        string logLine = FormatStatusLogLine(statusRequest);
        job.Log = string.IsNullOrEmpty(job.Log) ? logLine : job.Log + Environment.NewLine + logLine;

        bool isJobLevel = string.IsNullOrEmpty(statusRequest.Part) || string.Equals(statusRequest.Part, "job", StringComparison.OrdinalIgnoreCase);
        DeploymentStatus previousStatus = job.Status;

        if (string.Equals(statusRequest.Status, "ko", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = DeploymentStatus.Error;
            job.CompletedAt = DateTime.UtcNow;
        }
        else if (isJobLevel
            && string.IsNullOrEmpty(statusRequest.CurrentStep)
            && string.Equals(statusRequest.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = DeploymentStatus.Success;
            job.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (previousStatus != job.Status && job.Status is DeploymentStatus.Success or DeploymentStatus.Error)
        {
            await PublishJobStatusNotificationAsync(job, cancellationToken);
        }

        return Ok(new ProtocolAnswer { Status = "ok" });
    }

    /// <summary>Même logique que <see cref="HandleSetStatusCoreAsync"/> pour un NetworkTaskJob
    /// (uuid préfixé "net-") — pas de notion de "part"/"file" pour un job réseau (pas de fichier à
    /// télécharger), donc tout "setStatus" reçu est de niveau job.</summary>
    private async Task<IActionResult> HandleSetNetworkJobStatusCoreAsync(int jobId, SetStatusRequest statusRequest, CancellationToken cancellationToken)
    {
        NetworkTaskJob? job = await db.NetworkTaskJobs
            .Include(j => j.Agent)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        string logLine = FormatStatusLogLine(statusRequest);
        job.Log = string.IsNullOrEmpty(job.Log) ? logLine : job.Log + Environment.NewLine + logLine;

        NetworkJobStatus previousStatus = job.Status;

        if (string.Equals(statusRequest.Status, "ko", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = NetworkJobStatus.Error;
            job.CompletedAt = DateTime.UtcNow;
        }
        else if (string.IsNullOrEmpty(statusRequest.CurrentStep) && string.Equals(statusRequest.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = NetworkJobStatus.Success;
            job.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (previousStatus != job.Status && job.Status is NetworkJobStatus.Success or NetworkJobStatus.Error)
        {
            await PublishNetworkJobStatusNotificationAsync(job, cancellationToken);
        }

        return Ok(new ProtocolAnswer { Status = "ok" });
    }

    /// <summary>Même logique que <see cref="HandleSetNetworkJobStatusCoreAsync"/> pour un
    /// WakeOnLanTaskJob (uuid préfixé "wol-") — un "setStatus" reçu clôt le job de l'agent relais
    /// lui-même (l'envoi effectif du/des magic packet(s)), pas les postes réveillés individuellement
    /// (fire-and-forget, aucun accusé de réception WoL n'existe côté protocole).</summary>
    private async Task<IActionResult> HandleSetWakeOnLanJobStatusCoreAsync(int jobId, SetStatusRequest statusRequest, CancellationToken cancellationToken)
    {
        WakeOnLanTaskJob? job = await db.WakeOnLanTaskJobs
            .Include(j => j.Agent)
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        string logLine = FormatStatusLogLine(statusRequest);
        job.Log = string.IsNullOrEmpty(job.Log) ? logLine : job.Log + Environment.NewLine + logLine;

        WakeOnLanJobStatus previousStatus = job.Status;

        if (string.Equals(statusRequest.Status, "ko", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = WakeOnLanJobStatus.Error;
            job.CompletedAt = DateTime.UtcNow;
        }
        else if (string.IsNullOrEmpty(statusRequest.CurrentStep) && string.Equals(statusRequest.Status, "ok", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = WakeOnLanJobStatus.Success;
            job.CompletedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        if (previousStatus != job.Status && job.Status is WakeOnLanJobStatus.Success or WakeOnLanJobStatus.Error)
        {
            await PublishWakeOnLanJobStatusNotificationAsync(job, cancellationToken);
        }

        return Ok(new ProtocolAnswer { Status = "ok" });
    }

    /// <summary>Déclenche l'événement "Réveil réseau réussi/en échec" sur transition finale du job — voir <see cref="PublishNetworkJobStatusNotificationAsync"/> pour l'équivalent réseau.</summary>
    private async Task PublishWakeOnLanJobStatusNotificationAsync(WakeOnLanTaskJob job, CancellationToken cancellationToken)
    {
        string eventKey = job.Status == WakeOnLanJobStatus.Success
            ? NotificationEventCatalog.EventSuccess
            : NotificationEventCatalog.EventError;

        Dictionary<string, string?> variables = new()
        {
            ["job.agent"] = job.Agent?.AgentName ?? job.Agent?.DeviceId,
            ["job.status"] = job.Status == WakeOnLanJobStatus.Success ? "Réussi" : "En erreur",
            ["job.log"] = job.Log,
            ["job.url"] = $"{Request.Scheme}://{Request.Host}/tools/deployments/wakeonlan",
        };

        await notificationDispatch.PublishAsync(NotificationEventCatalog.WakeOnLanTaskJob, eventKey, job.Id, variables, cancellationToken);
    }

    /// <summary>Déclenche l'événement "Tâche réseau réussie/en échec" sur transition finale du job — voir <see cref="PublishJobStatusNotificationAsync"/> pour l'équivalent Deploy.</summary>
    private async Task PublishNetworkJobStatusNotificationAsync(NetworkTaskJob job, CancellationToken cancellationToken)
    {
        string eventKey = job.Status == NetworkJobStatus.Success
            ? NotificationEventCatalog.EventSuccess
            : NotificationEventCatalog.EventError;

        Dictionary<string, string?> variables = new()
        {
            ["job.agent"] = job.Agent?.AgentName ?? job.Agent?.DeviceId,
            ["job.status"] = job.Status == NetworkJobStatus.Success ? "Réussi" : "En erreur",
            ["job.log"] = job.Log,
            ["job.url"] = $"{Request.Scheme}://{Request.Host}/tools/deployments/networktasks",
        };

        await notificationDispatch.PublishAsync(NotificationEventCatalog.NetworkTaskJob, eventKey, job.Id, variables, cancellationToken);
    }

    /// <summary>Déclenche l'événement "Déploiement réussi/en échec" (voir NotificationEventCatalog.DeploymentJob) sur transition finale du job.</summary>
    private async Task PublishJobStatusNotificationAsync(DeploymentJob job, CancellationToken cancellationToken)
    {
        string eventKey = job.Status == DeploymentStatus.Success
            ? NotificationEventCatalog.EventSuccess
            : NotificationEventCatalog.EventError;

        Dictionary<string, string?> variables = new()
        {
            ["job.package"] = job.Package?.Name,
            ["job.agent"] = job.Agent?.AgentName ?? job.Agent?.DeviceId,
            ["job.computer"] = job.Agent?.Computer?.Name,
            ["job.status"] = job.Status == DeploymentStatus.Success ? "Réussi" : "En erreur",
            ["job.log"] = job.Log,
            ["job.url"] = $"{Request.Scheme}://{Request.Host}/tools/deployments/supervision",
        };

        await notificationDispatch.PublishAsync(NotificationEventCatalog.DeploymentJob, eventKey, job.Id, variables, cancellationToken);
    }

    /// <summary>Formate un rapport "setStatus" en une ligne de journal lisible (affichée telle quelle dans Supervision.razor).</summary>
    private static string FormatStatusLogLine(SetStatusRequest request)
    {
        List<string> tags = [$"[{DateTime.Now:HH:mm:ss}]"];

        if (!string.IsNullOrEmpty(request.CurrentStep))
        {
            tags.Add($"[{request.CurrentStep}]");
        }

        if (request.CheckNum is { } checkNum)
        {
            tags.Add($"check#{checkNum + 1}");
        }

        if (request.ActionNum is { } actionNum)
        {
            tags.Add($"action#{actionNum + 1}");
        }

        if (!string.IsNullOrEmpty(request.Sha512))
        {
            tags.Add($"file:{request.Sha512[..Math.Min(12, request.Sha512.Length)]}");
        }

        string line = string.Join(" ", tags);

        if (!string.IsNullOrEmpty(request.Msg))
        {
            line += $" {request.Msg}";
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            line += $" ({request.Status})";
        }

        return line;
    }

    /// <summary>
    /// Capture le header HTTP "User-Agent" et l'adresse IP distante de la requête courante,
    /// affichés dans l'onglet "Informations d'inventaire" de la fiche ordinateur (champs
    /// "UserAgent" et "Adresse publique de contact" de GLPI).
    /// </summary>
    private void UpdateAgentRequestMetadata(GlpiAgent agent)
    {
        agent.LastUserAgent = Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;
        agent.LastContactIp = HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Le réglage "Fréquence de l'inventaire (en heures)" de /admin/inventory pilote la
    /// valeur "expiration" du protocole COMMON, qui indique à l'agent GLPI son délai avant
    /// prochain contact (ex. "24h") — c'est le seul mécanisme du protocole qui exprime cette
    /// fréquence côté serveur.
    /// </summary>
    private async Task<string> GetExpirationAsync(CancellationToken cancellationToken)
    {
        InventorySettings settings = await settingsStore.ReadSectionAsync<InventorySettings>("InventorySettings", cancellationToken);
        return FormatExpiration(settings.InventoryFrequencyHours);
    }

    private static string FormatExpiration(int hours) => $"{Math.Max(hours, 1)}h";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// L'agent GLPI compresse parfois le corps (PROLOG XML comme requêtes JSON) en zlib ou
    /// gzip. Le protocole GLPI-Agent signale historiquement ça via un Content-Type dédié
    /// ("application/x-compress-zlib" / "application/x-compress-gzip") plutôt que le
    /// Content-Encoding HTTP standard — mais certaines versions d'agent (ou un proxy
    /// intermédiaire) utilisent bien l'en-tête Content-Encoding standard. Sans ce second
    /// contrôle, un corps ainsi compressé était lu tel quel comme texte UTF-8 (donc du binaire
    /// illisible), échouait au parsing JSON et l'inventaire était silencieusement rejeté (400)
    /// sans que l'ordinateur n'apparaisse jamais — d'où la vérification des deux en-têtes ici.
    /// </summary>
    private static string DecompressBody(byte[] bodyBytes, string? contentType, string? contentEncoding)
    {
        using MemoryStream compressed = new(bodyBytes);
        Stream? decompressor = (contentType, contentEncoding) switch
        {
            _ when contentType?.Contains("zlib", StringComparison.OrdinalIgnoreCase) == true
                || contentEncoding?.Contains("zlib", StringComparison.OrdinalIgnoreCase) == true
                => new ZLibStream(compressed, CompressionMode.Decompress),
            _ when contentType?.Contains("gzip", StringComparison.OrdinalIgnoreCase) == true
                || contentEncoding?.Contains("gzip", StringComparison.OrdinalIgnoreCase) == true
                => new GZipStream(compressed, CompressionMode.Decompress),
            _ => null
        };

        if (decompressor is null)
        {
            return Encoding.UTF8.GetString(bodyBytes);
        }

        using (decompressor)
        using (StreamReader reader = new(decompressor, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }
}

// --- DTOs de protocole (formes JSON exactes attendues par l'agent) ---

public class ProtocolAnswer
{
    public required string Status { get; set; }
    public string? Message { get; set; }
    public string? Expiration { get; set; }
    public string? Token { get; set; }
}

public class ContactRequest
{
    [System.Text.Json.Serialization.JsonPropertyName("action")]
    public string? Action { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("deviceid")]
    public required string DeviceId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("version")]
    public string? Version { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("installed-tasks")]
    public string[]? InstalledTasks { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("enabled-tasks")]
    public string[]? EnabledTasks { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("tag")]
    public string? Tag { get; set; }
}

/// <summary>
/// Corps attendu pour l'action "setStatus" — GLPI-Agent en envoie plusieurs par job (démarrage,
/// résultat de chaque check, progression de chaque téléchargement, préparation, log de chaque
/// action, fin de job), pas un seul rapport final. Noms de champs et sémantique vérifiés dans
/// les sources de glpi-agent (GLPI::Agent::Task::Deploy::Job::setStatus) : "status" ne vaut
/// jamais "success"/"error" mais "ok"/"ko" (parfois "warning"/"info" pour un check non bloquant),
/// et le message est porté par "msg", pas "message".
/// </summary>
public class SetStatusRequest
{
    public string? Uuid { get; set; }

    /// <summary>"job" (déclaration par défaut côté agent) ou "file" (progression d'un téléchargement, voir <see cref="Sha512"/>).</summary>
    public string? Part { get; set; }

    /// <summary>Hash du fichier concerné, uniquement quand <see cref="Part"/> vaut "file".</summary>
    public string? Sha512 { get; set; }

    public string? Status { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("actionnum")]
    public int? ActionNum { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("checknum")]
    public int? CheckNum { get; set; }

    public string? Msg { get; set; }

    /// <summary>
    /// Phase en cours ("checking"/"downloading"/"prepare"/"processing"). Absent uniquement sur
    /// le tout dernier message d'un job réussi (l'agent vide currentStep avant de l'envoyer,
    /// voir Job::currentStep('end')) — c'est le seul signal fiable de fin de job côté agent,
    /// utilisé par <see cref="AgentController.HandleSetStatusAsync"/> pour distinguer ce message
    /// des nombreux "status: ok" intermédiaires (qui portent tous un currentStep renseigné).
    /// </summary>
    public string? CurrentStep { get; set; }
}

public class DeployJobRef
{
    public required string Task { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("jobid")]
    public required string JobId { get; set; }
}

public class ContactAnswer
{
    public required string Status { get; set; }
    public string? Message { get; set; }
    public required string Expiration { get; set; }
    public Dictionary<string, List<DeployJobRef>>? Jobs { get; set; }
}
