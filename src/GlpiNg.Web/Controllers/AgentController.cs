using System.Text.Json;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Services;
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
/// Non couvert pour l'instant : compression (zlib/gzip/br), chiffrement (GLPI-CryptoKey-ID),
/// PROLOG legacy XML (fallback historique FusionInventory), proxy agent (GLPI-Proxy-ID).
/// </summary>
[ApiController]
[Route("glpi-agent")]
[Consumes("application/json")]
[Produces("application/json")]
public class AgentController(GlpiNgDbContext db, InventoryImportService inventoryImport) : ControllerBase
{
    private const string AgentIdHeader = "GLPI-Agent-ID";
    private const string RequestIdHeader = "GLPI-Request-ID";

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        // Le header GLPI-Agent-ID est l'identité réelle et stable de l'agent (protocole COMMON).
        if (!Request.Headers.TryGetValue(AgentIdHeader, out Microsoft.Extensions.Primitives.StringValues agentIdValues)
            || string.IsNullOrWhiteSpace(agentIdValues.ToString()))
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "missing GLPI-Agent-ID header" });
        }

        string agentUuid = agentIdValues.ToString();

        // On lit le corps une seule fois en JsonDocument pour dispatcher sur "action"
        // avant de désérialiser vers le DTO précis (contact vs inventory ont des formes différentes).
        using JsonDocument document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        string action = document.RootElement.TryGetProperty("action", out JsonElement actionEl)
            ? actionEl.GetString() ?? "inventory"
            : "inventory"; // défaut du protocole COMMON quand "action" est absent

        IActionResult result = action switch
        {
            "contact" => await HandleContactAsync(agentUuid, document, cancellationToken),
            "inventory" => await HandleInventoryAsync(agentUuid, document, cancellationToken),
            _ => BadRequest(new ProtocolAnswer { Status = "error", Message = $"unsupported action '{action}'" })
        };

        // Le header GLPI-Request-ID, quand présent, doit être renvoyé tel quel (protocole COMMON).
        if (Request.Headers.TryGetValue(RequestIdHeader, out Microsoft.Extensions.Primitives.StringValues reqId))
        {
            Response.Headers[RequestIdHeader] = reqId;
        }
        Response.Headers[AgentIdHeader] = agentUuid;

        return result;
    }

    private async Task<IActionResult> HandleContactAsync(string agentUuid, JsonDocument document, CancellationToken cancellationToken)
    {
        ContactRequest? contactRequest = document.Deserialize<ContactRequest>(JsonOptions);
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
        agent.AgentName = contactRequest.Name;
        agent.AgentVersion = contactRequest.Version;
        agent.Tag = contactRequest.Tag;
        agent.InstalledTasks = contactRequest.InstalledTasks ?? [];
        agent.EnabledTasks = contactRequest.EnabledTasks ?? agent.InstalledTasks;
        agent.LastContactAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        List<DeployJobRef> pendingDeployJobs = await db.DeploymentJobs
            .Where(j => j.AgentId == agent.Id && j.Status == DeploymentStatus.Pending)
            .Select(j => new DeployJobRef { Task = "deploy", JobId = j.Id.ToString() })
            .ToListAsync(cancellationToken);

        var answer = new ContactAnswer
        {
            Status = "ok",
            Expiration = "1d",
            Jobs = pendingDeployJobs.Count > 0
                ? new Dictionary<string, List<DeployJobRef>> { ["deploy"] = pendingDeployJobs }
                : null
        };

        return Ok(answer);
    }

    private async Task<IActionResult> HandleInventoryAsync(string agentUuid, JsonDocument document, CancellationToken cancellationToken)
    {
        InventoryRequest? inventoryRequest = document.Deserialize<InventoryRequest>(JsonOptions);
        if (inventoryRequest is null)
        {
            return BadRequest(new ProtocolAnswer { Status = "error", Message = "bad-format" });
        }

        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == agentUuid, cancellationToken);
        if (agent is null)
        {
            // Un agent peut en théorie envoyer un inventory sans contact préalable
            // (ex: injection manuelle) — on le crée à la volée plutôt que de rejeter l'inventaire.
            agent = new GlpiAgent { AgentUuid = agentUuid, DeviceId = inventoryRequest.DeviceId };
            db.Agents.Add(agent);
        }

        await inventoryImport.ImportAsync(agent, inventoryRequest.Content, cancellationToken);

        return Ok(new ProtocolAnswer { Status = "ok", Expiration = "1d" });
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
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
