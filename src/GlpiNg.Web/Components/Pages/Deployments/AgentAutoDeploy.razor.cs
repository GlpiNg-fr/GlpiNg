using BlazorBootstrap;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Web.Controllers;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Deployments;

/// <summary>
/// « Déploiement automatique » : règle l'équipement automatique des matériels repérés par la
/// découverte réseau. L'installation elle-même est menée par
/// <see cref="AgentAutoDeployCronTask"/>, cette page n'en est que le formulaire.
/// </summary>
public partial class AgentAutoDeploy : ComponentBase
{
    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private ConfigHistoryService History { get; set; } = null!;

    [Inject]
    private AuthSecretProtector Protector { get; set; } = null!;

    [Inject]
    private ToastService ToastService { get; set; } = null!;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    private AgentAutoDeploySettings? _model;
    private List<DiscoveredNetworkDevice> _pending = [];
    private string _password = string.Empty;
    private bool _hasPassword;
    private bool _hasInstaller;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        _model = await SettingsStore.ReadSectionAsync<AgentAutoDeploySettings>(AgentAutoDeployCronTask.SectionName);
        _hasPassword = !string.IsNullOrEmpty(_model.AdminPasswordProtected);

        AgentInstallSettings install = await SettingsStore.ReadSectionAsync<AgentInstallSettings>(AgentInstallerController.SectionName);
        _hasInstaller = !string.IsNullOrWhiteSpace(install.InstallerFileName);

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _pending = await db.DiscoveredNetworkDevices
            .Where(device => device.AgentDeployLastAttemptAt != null)
            .OrderByDescending(device => device.AgentDeployLastAttemptAt)
            .Take(50)
            .ToListAsync();
    }

    private async Task SaveAsync()
    {
        if (_model is null)
        {
            return;
        }

        _isSaving = true;

        try
        {
            AgentAutoDeploySettings before = await SettingsStore.ReadSectionAsync<AgentAutoDeploySettings>(AgentAutoDeployCronTask.SectionName);

            // Un champ laissé vide conserve le mot de passe déjà enregistré : il n'est jamais
            // réaffiché, donc le vider ne peut pas être une volonté de l'effacer.
            if (!string.IsNullOrEmpty(_password))
            {
                _model.AdminPasswordProtected = Protector.Protect(_password);
                _password = string.Empty;
                _hasPassword = true;
            }

            await SettingsStore.SaveSectionAsync(AgentAutoDeployCronTask.SectionName, _model);

            // Le mot de passe chiffré n'a rien à faire dans l'historique de configuration : la
            // comparaison se fait sur des copies dont il est retiré.
            await History.AppendAsync("Administrateur", SettingsDiff.Compare(WithoutSecret(before), WithoutSecret(_model)));

            ToastService.Notify(new ToastMessage(ToastType.Success, Tr.T("Configuration enregistrée.")));
        }
        catch (IOException ex)
        {
            ToastService.Notify(new ToastMessage(ToastType.Danger, Tr.T("Échec de l'enregistrement : {0}", ex.Message)));
        }
        finally
        {
            _isSaving = false;
        }
    }

    private static AgentAutoDeploySettings WithoutSecret(AgentAutoDeploySettings settings)
        => new()
        {
            Enabled = settings.Enabled,
            AdminUser = settings.AdminUser,
            MaxPerRun = settings.MaxPerRun,
            MaxAttempts = settings.MaxAttempts,
            SkipSnmpDevices = settings.SkipSnmpDevices,
            HostnameContains = settings.HostnameContains,
            Exclusions = settings.Exclusions,
        };
}
