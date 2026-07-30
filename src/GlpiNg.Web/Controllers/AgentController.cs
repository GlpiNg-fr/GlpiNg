using System.Text.Json.Nodes;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Endpoint compatible GLPI-Agent. Reçoit les requêtes "contact" (annonce périodique),
/// "getJobs" (récupération d'une tâche de déploiement) et "setStatus" (rapport d'exécution),
/// et expose le téléchargement des fichiers de package associés.
/// </summary>
[ApiController]
[Route("glpi-agent")]
public class AgentController(GlpiNgDbContext db, DeployJobJsonBuilder deployJobJsonBuilder, IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Point d'entrée générique de l'agent. Le champ "action" du corps détermine le
    /// traitement ("contact" par défaut, "getJobs", "setStatus").
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Handle([FromBody] AgentRequest request)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.DeviceId == request.DeviceId);

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

        string action = request.Action ?? "contact";

        return action switch
        {
            "getJobs" => await HandleGetJobs(agent),
            "setStatus" => await HandleSetStatus(request),
            _ => await HandleContact(agent)
        };
    }

    /// <summary>
    /// Téléchargement d'un fichier de package référencé par son hash SHA512
    /// (présent dans le tableau "associatedFiles" du job renvoyé par getJobs).
    /// </summary>
    [HttpGet("deploy/file/{sha512}")]
    public async Task<IActionResult> GetDeployFile(string sha512)
    {
        DeploymentPackageFile? file = await db.DeploymentPackageFiles
            .FirstOrDefaultAsync(f => f.Sha512 == sha512);

        if (file is null)
        {
            return NotFound();
        }

        string rootPath = configuration["PackageStorage:RootPath"] ?? "PackageStorage";
        string fullPath = Path.Combine(rootPath, file.StoragePath);

        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        FileStream stream = System.IO.File.OpenRead(fullPath);
        return File(stream, "application/octet-stream", file.FileName);
    }

    private async Task<IActionResult> HandleContact(GlpiAgent agent)
    {
        List<int> pendingJobIds = await db.DeploymentJobs
            .Where(j => j.AgentId == agent.Id && j.Status == DeploymentStatus.Pending)
            .Select(j => j.Id)
            .ToListAsync();

        return Ok(new AgentContactResponse
        {
            Tasks = pendingJobIds.Count > 0 ? ["deploy"] : ["inventory"]
        });
    }

    private async Task<IActionResult> HandleGetJobs(GlpiAgent agent)
    {
        DeploymentJob? job = await db.DeploymentJobs
            .Include(j => j.Package)
            .ThenInclude(p => p!.Files)
            .Where(j => j.AgentId == agent.Id && j.Status == DeploymentStatus.Pending)
            .OrderBy(j => j.CreatedAt)
            .FirstOrDefaultAsync();

        if (job is null || job.Package is null)
        {
            return Ok(new JsonObject { ["jobs"] = new JsonArray() });
        }

        string jobUuid = job.Id.ToString("D8");
        JsonObject payload = deployJobJsonBuilder.Build(job, job.Package, jobUuid);

        job.Status = DeploymentStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Content(payload.ToJsonString(), "application/json");
    }

    private async Task<IActionResult> HandleSetStatus(AgentRequest request)
    {
        if (string.IsNullOrEmpty(request.Uuid) || !int.TryParse(request.Uuid, out int jobId))
        {
            return BadRequest();
        }

        DeploymentJob? job = await db.DeploymentJobs.FirstOrDefaultAsync(j => j.Id == jobId);
        if (job is null)
        {
            return NotFound();
        }

        if (string.Equals(request.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = DeploymentStatus.Success;
            job.CompletedAt = DateTime.UtcNow;
        }
        else if (string.Equals(request.Status, "error", StringComparison.OrdinalIgnoreCase))
        {
            job.Status = DeploymentStatus.Error;
            job.CompletedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(request.Message))
        {
            job.Log = string.IsNullOrEmpty(job.Log)
                ? request.Message
                : job.Log + Environment.NewLine + request.Message;
        }

        await db.SaveChangesAsync();
        return Ok();
    }
}

public class AgentRequest
{
    public required string DeviceId { get; set; }
    public string? Hostname { get; set; }
    public string? AgentVersion { get; set; }
    public string? Action { get; set; }
    public string? Uuid { get; set; }
    public string? Status { get; set; }
    public string? Message { get; set; }
}

public class AgentContactResponse
{
    public required string[] Tasks { get; set; }
}
