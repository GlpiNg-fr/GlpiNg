using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Agent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Endpoint compatible GLPI-Agent. Reçoit les requêtes "contact" (annonce périodique)
/// et répond avec la liste des tâches à exécuter (inventory, deploy...).
///
/// NB: squelette initial — le parsing exhaustif des payloads (contact/inventory/deploy)
/// reste à faire en s'alignant sur le format JSON réel de l'agent.
/// </summary>
[ApiController]
[Route("glpi-agent")]
public class AgentController(GlpiNgDbContext db) : ControllerBase
{
    /// <summary>
    /// Point d'entrée générique de l'agent. Le contenu du corps détermine le type
    /// de requête ("query": "contact" | "inventory" | ...).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Handle([FromBody] AgentContactRequest request)
    {
        var agent = await db.Agents.FirstOrDefaultAsync(a => a.DeviceId == request.DeviceId);

        if (agent is null)
        {
            agent = new GlpiAgent
            {
                DeviceId = request.DeviceId,
                Hostname = request.Hostname,
                AgentVersion = request.AgentVersion
            };
            db.Agents.Add(agent);
        }
        else
        {
            agent.LastContactAt = DateTime.UtcNow;
            agent.Hostname = request.Hostname ?? agent.Hostname;
            agent.AgentVersion = request.AgentVersion ?? agent.AgentVersion;
        }

        await db.SaveChangesAsync();

        // TODO: déterminer les tâches à proposer (inventory systématique,
        // deploy si des DeploymentJob "Pending" existent pour cet agent).
        var pendingTasks = await db.DeploymentJobs
            .Where(j => j.AgentId == agent.Id && j.Status == DeploymentStatus.Pending)
            .Select(j => j.Id)
            .ToListAsync();

        return Ok(new AgentContactResponse
        {
            Tasks = pendingTasks.Count > 0 ? ["deploy"] : ["inventory"]
        });
    }
}

public class AgentContactRequest
{
    public required string DeviceId { get; set; }
    public string? Hostname { get; set; }
    public string? AgentVersion { get; set; }
}

public class AgentContactResponse
{
    public required string[] Tasks { get; set; }
}
