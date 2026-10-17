using System.Net;
using GlpiNg.Modules.Abstractions.Localization;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using GlpiNg.Web.Controllers;
using GlpiNg.Web.Data;
using GlpiNg.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Components.Pages.Deployments;

/// <summary>
/// « Déployer l'agent » : installe GLPI-Agent sur une liste de postes saisie à la main,
/// importée d'un CSV ou tirée d'un annuaire LDAP, avec les réglages de la page
/// <see cref="AgentInstall"/>. Le déploiement lui-même est l'affaire d'<see cref="AgentInstallService"/>.
/// </summary>
public partial class AgentDeploy : ComponentBase, IDisposable
{
    /// <summary>1 Mo : une liste de postes, pas un inventaire.</summary>
    private const long MaxCsvBytes = 1024 * 1024;

    [Inject]
    private SettingsCacheService SettingsStore { get; set; } = null!;

    [Inject]
    private AgentInstallService InstallService { get; set; } = null!;

    [Inject]
    private LdapAuthenticationService Ldap { get; set; } = null!;

    [Inject]
    private IDbContextFactory<GlpiNgDbContext> DbFactory { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    private AgentInstallSettings? _settings;
    private string _targetsText = string.Empty;
    private string _user = string.Empty;
    private string _password = string.Empty;

    private List<(int Id, string Name)> _ldapServers = [];
    private int _ldapServerId;
    private string _ldapTerm = string.Empty;
    private IReadOnlyList<string> _ldapHosts = [];
    private string? _ldapError;
    private bool _isSearching;

    private readonly List<AgentInstallOutcome> _results = [];
    private CancellationTokenSource? _cancellation;
    private bool _isRunning;

    private bool CanDeploy => Targets().Count > 0 && !string.IsNullOrWhiteSpace(_user) && !_isRunning;

    protected override async Task OnInitializedAsync()
    {
        _settings = await SettingsStore.ReadSectionAsync<AgentInstallSettings>(AgentInstallerController.SectionName);

        await using GlpiNgDbContext db = await DbFactory.CreateDbContextAsync();
        _ldapServers = await db.AuthLdapServers.AsNoTracking()
            .Where(server => server.IsActive)
            .OrderByDescending(server => server.IsDefault)
            .ThenBy(server => server.Name)
            .Select(server => new ValueTuple<int, string>(server.Id, server.Name))
            .ToListAsync();

        _ldapServerId = _ldapServers.Count > 0 ? _ldapServers[0].Id : 0;
    }

    /// <summary>Les postes effectivement visés : une ligne non vide = un poste, sans doublon.</summary>
    private List<string> Targets() =>
    [
        .. _targetsText
            .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
    ];

    private async Task ImportCsvAsync(InputFileChangeEventArgs args)
    {
        await using Stream content = args.File.OpenReadStream(MaxCsvBytes);
        using StreamReader reader = new(content);

        List<string> hosts = [];

        while (await reader.ReadLineAsync() is { } line)
        {
            // Première colonne seulement : le reste d'un export (OS, utilisateur, lieu...) ne
            // nous regarde pas. Une virgule ou un point-virgule sépare, selon l'outil d'origine.
            string host = line.Split([',', ';'], 2)[0].Trim().Trim('"');

            // Ligne d'en-tête : pas de nom d'hôte sérieux qui s'appelle « host » ou « nom ».
            if (host.Length == 0 || hosts.Count == 0 && IsHeader(host))
            {
                continue;
            }

            hosts.Add(host);
        }

        Add(hosts);
    }

    private static bool IsHeader(string value)
        => value.Equals("host", StringComparison.OrdinalIgnoreCase)
            || value.Equals("hostname", StringComparison.OrdinalIgnoreCase)
            || value.Equals("nom", StringComparison.OrdinalIgnoreCase)
            || value.Equals("name", StringComparison.OrdinalIgnoreCase)
            || value.Equals("ip", StringComparison.OrdinalIgnoreCase);

    private async Task SearchLdapAsync()
    {
        _isSearching = true;
        _ldapError = null;

        try
        {
            (IReadOnlyList<string> hosts, string? error) = await Ldap.SearchComputersAsync(_ldapServerId, _ldapTerm, CancellationToken.None);
            _ldapHosts = hosts;
            _ldapError = error;
        }
        finally
        {
            _isSearching = false;
        }
    }

    private void AddLdapHosts() => Add(_ldapHosts);

    /// <summary>Ajoute des postes à la zone de saisie, qui reste la seule liste de référence.</summary>
    private void Add(IEnumerable<string> hosts)
    {
        List<string> all = [.. Targets(), .. hosts];
        _targetsText = string.Join('\n', all.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private void Cancel() => _cancellation?.Cancel();

    private async Task DeployAsync()
    {
        if (_settings is null || string.IsNullOrEmpty(_settings.DownloadToken))
        {
            return;
        }

        _isRunning = true;
        _results.Clear();
        _cancellation = new CancellationTokenSource();

        Uri installerUrl = new(new Uri(Navigation.BaseUri), $"inventory/agent-installer/{_settings.DownloadToken}");
        NetworkCredential credential = BuildCredential(_user, _password);

        try
        {
            // ponytail: un poste après l'autre. Suffisant pour une salle ou un service ; le jour
            // où l'on déploie sur mille postes, passer à Parallel.ForEachAsync avec un degré de
            // parallélisme réglable (et un suivi en base plutôt qu'en mémoire de page).
            foreach (string host in Targets())
            {
                if (_cancellation.IsCancellationRequested)
                {
                    break;
                }

                _results.Add(await InstallService.DeployAsync(_settings, host, installerUrl, credential, _cancellation.Token));
                StateHasChanged();
            }
        }
        catch (OperationCanceledException)
        {
            _results.Add(AgentInstallOutcome.Failed(string.Empty, Tr.T("Déploiement interrompu.")));
        }
        finally
        {
            _isRunning = false;
            _cancellation.Dispose();
            _cancellation = null;
        }
    }

    /// <summary>
    /// Sépare le domaine de l'identifiant : Negotiate/NTLM veut les deux à part, et un compte
    /// de domaine se saisit naturellement « DOMAINE\compte ». Un UPN (compte@domaine.fr) part
    /// tel quel, c'est la forme que le serveur distant sait résoudre lui-même.
    /// </summary>
    private static NetworkCredential BuildCredential(string user, string password)
    {
        string[] parts = user.Split('\\', 2);

        return parts.Length == 2
            ? new NetworkCredential(parts[1], password, parts[0])
            : new NetworkCredential(user, password);
    }

    public void Dispose() => _cancellation?.Dispose();
}
