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
/// Non couvert pour l'instant : compression brotli, chiffrement (GLPI-CryptoKey-ID),
/// proxy agent (GLPI-Proxy-ID). La compression zlib/gzip (Content-Type
/// "application/x-compress-zlib"/"-gzip") est décompressée en entrée. Le PROLOG legacy XML
/// (probe FusionInventory/OCS envoyé avant bascule sur le protocole JSON natif) est accepté
/// en entrée mais toujours répondu en JSON.
/// </summary>
[ApiController]
[Route("glpi-agent")]
[Produces("application/json")]
[AllowAnonymous]
public class AgentController(
    GlpiNgDbContext db,
    InventoryImportService inventoryImport,
    DeployJobJsonBuilder deployJobJsonBuilder,
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
            string expiration = await GetExpirationAsync(cancellationToken);
            string serverUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/glpi-agent";

            return Ok(new
            {
                status = "ok",
                expiration,
                schedule = new[]
                {
                    new { task = "inventory", remote = serverUrl },
                    new { task = "Deploy", remote = serverUrl }
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
            logger.LogInformation("glpi-agent: PROLOG XML historique reçu, réponse JSON pour basculer l'agent sur le protocole natif.");
            return Ok(new ProtocolAnswer { Status = "ok", Expiration = await GetExpirationAsync(cancellationToken) });
        }

        // Le header GLPI-Agent-ID est l'identité réelle et stable de l'agent (protocole COMMON).
        if (!Request.Headers.TryGetValue(AgentIdHeader, out Microsoft.Extensions.Primitives.StringValues agentIdValues)
            || string.IsNullOrWhiteSpace(agentIdValues.ToString()))
        {
            logger.LogWarning("glpi-agent 400: missing {Header} header. Headers reçus: {Headers}",
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
            logger.LogWarning(ex, "glpi-agent 400: corps non-JSON (Content-Type={ContentType}, Content-Encoding={ContentEncoding}, {Length} octets). Début du corps: {Preview}",
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
                "setStatus" => await HandleSetStatusAsync(document, cancellationToken),
                _ => BadRequest(new ProtocolAnswer { Status = "error", Message = $"unsupported action '{action}'" })
            };

            if (result is BadRequestObjectResult badRequest)
            {
                logger.LogWarning("glpi-agent 400 sur action {Action}: {Answer}. Corps reçu: {Body}",
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
    /// le téléchargement du fichier entier reconstitué.
    /// </summary>
    [HttpGet("deploy/file/part/{sha512}")]
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
            logger.LogWarning(ex, "glpi-agent 400 (contact): échec de désérialisation. Corps: {Body}", document.RootElement.GetRawText());
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

        var answer = new ContactAnswer
        {
            Status = "ok",
            Expiration = await GetExpirationAsync(cancellationToken),
            Jobs = pendingDeployJobs.Count > 0
                ? new Dictionary<string, List<DeployJobRef>> { ["deploy"] = pendingDeployJobs }
                : null
        };

        return Ok(answer);
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
            logger.LogWarning(ex, "glpi-agent 400 (inventory): échec de désérialisation. Clés reçues à la racine: {Keys}",
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
            logger.LogInformation("glpi-agent: inventaire désactivé dans /admin/inventory, requête ignorée pour l'agent {AgentUuid}.", agentUuid);
            return Ok(new ProtocolAnswer { Status = "ok", Expiration = expiration });
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

        await inventoryImport.ImportAsync(agent, inventoryRequest.Content, cancellationToken);

        return Ok(new ProtocolAnswer { Status = "ok", Expiration = expiration });
    }

    private async Task<IActionResult> HandleGetJobsAsync(string agentUuid, CancellationToken cancellationToken)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        return await HandleGetJobsCoreAsync(agent, cancellationToken);
    }

    /// <summary>Cœur commun aux deux façons dont un agent peut demander ses jobs de déploiement :
    /// "getJobs" en POST (identifié par l'en-tête GLPI-Agent-ID, voir <see cref="HandleGetJobsAsync"/>)
    /// et "getJobs" en GET (identifié par le paramètre "machineid", voir <see cref="HandleGet"/>).</summary>
    private async Task<IActionResult> HandleGetJobsCoreAsync(GlpiAgent? agent, CancellationToken cancellationToken)
    {
        if (agent is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
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
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        string jobUuid = job.Id.ToString("D8");
        JsonObject payload = deployJobJsonBuilder.Build(job, job.Package, jobUuid);

        job.Status = DeploymentStatus.Running;
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
            logger.LogWarning(ex, "glpi-agent 400 (setStatus): échec de désérialisation. Corps: {Body}", document.RootElement.GetRawText());
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
    private async Task<IActionResult> HandleSetStatusCoreAsync(SetStatusRequest statusRequest, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(statusRequest.Uuid) || !int.TryParse(statusRequest.Uuid, out int jobId))
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
