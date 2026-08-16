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
using GlpiNg.Web.Services;
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
    ILogger<AgentController> logger) : ControllerBase
{
    private const string AgentIdHeader = "GLPI-Agent-ID";
    private const string RequestIdHeader = "GLPI-Request-ID";

    /// <summary>
    /// L'agent GLPI envoie un GET ?action=getConfig en tout premier contact pour découvrir
    /// les capacités du serveur et obtenir l'URL de soumission d'inventaire. Sans réponse JSON
    /// valide ici, l'agent ne passera jamais au protocole POST (contact/inventory) et aucun
    /// ordinateur ne sera créé.
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
                    new { task = "inventory", remote = serverUrl }
                }
            });
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

    private async Task<IActionResult> HandleSetStatusAsync(JsonDocument document, CancellationToken cancellationToken)
    {
        SetStatusRequest? statusRequest = document.Deserialize<SetStatusRequest>(JsonOptions);
        if (statusRequest is null || string.IsNullOrEmpty(statusRequest.Uuid) || !int.TryParse(statusRequest.Uuid, out int jobId))
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        DeploymentJob? job = await db.DeploymentJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        if (string.Equals(statusRequest.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = DeploymentStatus.Success;
            job.CompletedAt = DateTime.UtcNow;
        }
        else if (string.Equals(statusRequest.Status, "error", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = DeploymentStatus.Error;
            job.CompletedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(statusRequest.Message))
        {
            job.Log = string.IsNullOrEmpty(job.Log)
                ? statusRequest.Message
                : job.Log + Environment.NewLine + statusRequest.Message;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new ProtocolAnswer { Status = "ok" });
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

/// <summary>Corps attendu pour l'action "setStatus" (rapport d'exécution d'un job de déploiement).</summary>
public class SetStatusRequest
{
    public string? Uuid { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
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
