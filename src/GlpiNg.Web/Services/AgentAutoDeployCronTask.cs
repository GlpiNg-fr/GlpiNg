using System.Net;
using GlpiNg.Modules.Abstractions.Cron;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using GlpiNg.Web.Controllers;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Installe GLPI-Agent sur les postes que la découverte réseau vient de repérer et qui n'ont
/// encore rien — l'équivalent automatique de la page « Déployer l'agent », réglé depuis
/// /tools/deployments/agent-auto (<see cref="AgentAutoDeploySettings"/>).
///
/// Dans l'hôte et non dans le module Déploiement : le mot de passe du compte d'administration
/// est déchiffré par <see cref="AuthSecretProtector"/> et les réglages sont lus par
/// <see cref="SettingsCacheService"/>, deux services de l'hôte.
///
/// Chaque tentative est inscrite sur l'équipement lui-même
/// (<see cref="DiscoveredNetworkDevice.AgentDeployAttempts"/>) : c'est ce qui empêche la tâche
/// de revenir indéfiniment sur ce qui n'est pas un poste Windows, et d'y épuiser un compte
/// d'administration.
/// </summary>
public sealed class AgentAutoDeployCronTask(
    GlpiNgDbContext db,
    SettingsCacheService settingsStore,
    AuthSecretProtector protector,
    AgentInstallService installService,
    EventLogService eventLog,
    ILogger<AgentAutoDeployCronTask> logger) : ICronTask
{
    /// <summary>Nom de la section d'<c>AppSettings</c> portant <see cref="AgentAutoDeploySettings"/>.</summary>
    public const string SectionName = "AgentAutoDeploySettings";

    public string Key => "agent_auto_deploy";

    public string Name => "Déploiement automatique de l'agent";

    public string Description =>
        "Installe GLPI-Agent sur les matériels repérés par la découverte réseau et encore dépourvus d'agent, "
        + "avec le fichier d'installation et les options de « Installation de l'agent ». "
        + "Sans effet tant que le déploiement automatique n'est pas activé dans « Déploiement automatique ».";

    /// <summary>
    /// Toutes les heures : une installation dure plusieurs minutes et la tâche n'en mène qu'un
    /// petit nombre par passage, rien ne presse pour un poste qui vient d'apparaître.
    /// </summary>
    public int DefaultFrequencyMinutes => 60;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        AgentAutoDeploySettings settings = await settingsStore.ReadSectionAsync<AgentAutoDeploySettings>(SectionName);

        if (!settings.Enabled)
        {
            return;
        }

        AgentInstallSettings install = await settingsStore.ReadSectionAsync<AgentInstallSettings>(AgentInstallerController.SectionName);
        Uri? installerUrl = AgentInstallService.InstallerUrl(install);
        string? password = protector.Unprotect(settings.AdminPasswordProtected);

        // Trois conditions sans lesquelles chaque tentative échouerait de toute façon, mais en
        // consommant au passage le crédit de tentatives des équipements visés.
        if (installerUrl is null || string.IsNullOrWhiteSpace(settings.AdminUser) || string.IsNullOrEmpty(password))
        {
            await eventLog.LogAsync("deployment", EventLogLevel.Warning,
                "Déploiement automatique de l'agent : activé, mais sans installeur, sans URL de serveur exploitable "
                + "ou sans compte d'administration. Rien n'a été tenté.",
                cancellationToken: cancellationToken);
            return;
        }

        List<DiscoveredNetworkDevice> candidates = await CandidatesAsync(settings, cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        NetworkCredential credential = Credential(settings.AdminUser, password);
        int installed = 0;

        foreach (DiscoveredNetworkDevice device in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Le nom d'hôte d'abord : une adresse obtenue en DHCP peut déjà avoir changé de main
            // entre la découverte et ce passage.
            string host = string.IsNullOrWhiteSpace(device.Hostname) ? device.IpAddress : device.Hostname;

            AgentInstallOutcome outcome = await installService.DeployAsync(install, host, installerUrl, credential, cancellationToken);

            device.AgentDeployAttempts++;
            device.AgentDeployLastAttemptAt = DateTime.UtcNow;
            device.AgentDeployLastError = outcome.Success ? null : outcome.Message;

            if (outcome.Success)
            {
                installed++;
            }
            else
            {
                logger.LogWarning("Déploiement automatique de l'agent échoué sur {Host} : {Message}", host, outcome.Message);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        string summary = $"Déploiement automatique de l'agent : {candidates.Count} matériel(s) tenté(s), {installed} équipé(s).";
        logger.LogInformation("{Summary}", summary);

        await eventLog.LogAsync("deployment", installed == 0 ? EventLogLevel.Warning : EventLogLevel.Info, summary,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Les matériels à équiper : jamais tentés, ou dont la dernière tentative a échoué sans avoir
    /// épuisé le nombre d'essais. Une tentative réussie se reconnaît à son absence d'erreur, et
    /// n'est jamais reprise — l'agent installé finit par remonter son inventaire, et c'est lui qui
    /// fera sortir le matériel de la liste des actifs non gérés.
    /// </summary>
    private async Task<List<DiscoveredNetworkDevice>> CandidatesAsync(AgentAutoDeploySettings settings, CancellationToken cancellationToken)
    {
        IQueryable<DiscoveredNetworkDevice> query = db.DiscoveredNetworkDevices
            .Where(device => device.Status == DiscoveredDeviceStatus.New
                && (device.AgentDeployLastAttemptAt == null
                    || device.AgentDeployLastError != null && device.AgentDeployAttempts < settings.MaxAttempts));

        if (settings.SkipSnmpDevices)
        {
            query = query.Where(device => device.SysDescr == null);
        }

        if (!string.IsNullOrWhiteSpace(settings.HostnameContains))
        {
            string term = settings.HostnameContains.Trim();
            query = query.Where(device => device.Hostname != null && device.Hostname.Contains(term));
        }

        List<DiscoveredNetworkDevice> candidates = await query
            .OrderBy(device => device.FirstSeenAt)
            .Take(Math.Max(1, settings.MaxPerRun))
            .ToListAsync(cancellationToken);

        HashSet<string> exclusions = new(
            (settings.Exclusions ?? string.Empty).Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        // Exclusions appliquées en mémoire : elles portent sur deux colonnes à la fois et la liste
        // ramenée est déjà bornée par MaxPerRun.
        return exclusions.Count == 0
            ? candidates
            : [.. candidates.Where(device => !exclusions.Contains(device.IpAddress)
                && (device.Hostname is null || !exclusions.Contains(device.Hostname)))];
    }

    /// <summary>Même séparation domaine/identifiant que la page de déploiement manuel.</summary>
    private static NetworkCredential Credential(string user, string password)
    {
        string[] parts = user.Split('\\', 2);

        return parts.Length == 2
            ? new NetworkCredential(parts[1], password, parts[0])
            : new NetworkCredential(user, password);
    }
}
