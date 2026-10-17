using System.Security.Cryptography;
using System.Text;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GlpiNg.Web.Controllers;

/// <summary>
/// Sert l'installeur de GLPI-Agent téléversé depuis /tools/deployments/agent-install, pour que
/// le poste distant le télécharge lui-même pendant son installation (voir
/// <see cref="AgentInstallService"/> : le canal WinRM ne convient pas au transfert du fichier).
///
/// <b>Anonyme par nécessité</b> : le poste n'a ni agent ni session applicative à ce moment-là.
/// Le jeton de l'URL est donc la seule protection — il est aléatoire (256 bits) et régénéré à
/// chaque téléversement. Monté sous <c>/inventory</c> comme <see cref="AgentController"/>, pour
/// les mêmes raisons : c'est le préfixe déjà exempté de l'écran de migrations en attente
/// (voir <c>MigrationsGateMiddleware</c>).
/// </summary>
[ApiController]
[Route("inventory")]
[AllowAnonymous]
public class AgentInstallerController(SettingsCacheService settingsStore, AgentInstallService installService) : ControllerBase
{
    /// <summary>Nom de la section d'<c>AppSettings</c> portant <see cref="AgentInstallSettings"/>.</summary>
    public const string SectionName = "AgentInstallSettings";

    [HttpGet("agent-installer/{token}")]
    public async Task<IActionResult> GetInstaller(string token, CancellationToken cancellationToken)
    {
        AgentInstallSettings settings = await settingsStore.ReadSectionAsync<AgentInstallSettings>(SectionName);

        if (string.IsNullOrEmpty(settings.DownloadToken)
            || string.IsNullOrWhiteSpace(settings.InstallerFileName)
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(settings.DownloadToken)))
        {
            return NotFound();
        }

        string path = installService.InstallerFullPath(settings.InstallerFileName);

        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "application/octet-stream", settings.InstallerFileName)
            : NotFound();
    }
}
