using GlpiNg.Modules.Abstractions.Deployment;
using GlpiNg.Modules.Abstractions.FieldUnicity;
using GlpiNg.Modules.Abstractions.Items;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Deployment.Services;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Modules.Inventory.Services;
using GlpiNg.Web.Data;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Services;

/// <summary>
/// Importe le contenu d'une requête "inventory" du protocole GLPI-Agent
/// dans les entités Computer / ComputerComponent, en le rattachant à l'agent fourni.
/// </summary>
public class InventoryImportService(
    GlpiNgDbContext db,
    SettingsCacheService settingsStore,
    NotificationDispatchService notificationDispatch,
    IComputerDeploymentAssignmentService deploymentAssignmentService,
    IFieldUnicityChecker fieldUnicity,
    EntityTreeCache entityTree,
    IHttpContextAccessor httpContextAccessor)
{
    private const string HistoryUser = "inventory";
    private const string SettingsSection = "InventorySettings";

    /// <summary>Un poste qui n'existe pas encore n'a aucun champ verrouillé : son identifiant n'existe pas, et il n'y a rien à protéger.</summary>
    private static readonly IReadOnlySet<string> EmptyLockedFields = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Résultat null : l'inventaire a été rejeté par une règle d'affectation à l'import (action RefuseImport) — voir BuildImportAssignmentContext.</summary>
    public async Task<Computer?> ImportAsync(GlpiAgent agent, InventoryContent content, CancellationToken cancellationToken = default)
    {
        InventorySettings settings = await settingsStore.ReadSectionAsync<InventorySettings>(SettingsSection, cancellationToken);

        // Règles d'affectation à l'import (voir ImportAssignmentRuleEngine) : évaluées avant toute
        // résolution/création du Computer, sur les seules informations brutes de la requête, pour
        // pouvoir refuser l'import sans avoir touché la base. Équivalent de RuleImportEntity dans GLPI.
        ImportAssignmentRuleContext importContext = BuildImportAssignmentContext(agent, content);
        List<ImportAssignmentRule> importRules = await LoadActiveImportAssignmentRulesAsync(cancellationToken);
        ImportAssignmentRuleResult importResult = ImportAssignmentRuleEngine.Evaluate(importContext, importRules);

        if (importResult.Refuse)
        {
            db.Set<RefusedImportLog>().Add(new RefusedImportLog
            {
                RuleName = importResult.MatchedRuleName ?? "?",
                ComputerName = importContext.ComputerName,
                SerialNumber = importContext.SerialNumber,
                Domain = importContext.Domain,
                Tag = importContext.Tag,
                IpAddress = importContext.IpAddresses.FirstOrDefault(ip => !string.IsNullOrWhiteSpace(ip)),
                AgentIdentifier = agent.DeviceId ?? agent.Hostname ?? agent.AgentUuid
            });

            // Sauvegarde tout de même le contact agent (déjà renseigné sur agent par l'appelant,
            // voir AgentController.UpdateAgentRequestMetadata) : seul le Computer est rejeté.
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        // Dictionnaires et liste noire : chargés avant la résolution du poste, parce que le contrôle
        // d'unicité ci-dessous compare des valeurs déjà normalisées. Les comparer avant passage des
        // dictionnaires laisserait un modèle réécrit (« LATITUDE 5540 » → « Latitude 5540 ») ne pas
        // se reconnaître comme doublon de ce qui est déjà en base.
        Dictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries = await LoadActiveDictionaryRulesAsync(cancellationToken);
        Dictionary<ImportBlacklistType, HashSet<string>> blacklist = await LoadBlacklistAsync(cancellationToken);

        Computer? computer = await WithInventoryCollections(db.Computers)
            .FirstOrDefaultAsync(c => c.AgentId == agent.Id, cancellationToken);

        string ruleName;
        string? inputValue;
        bool isNew = false;

        if (computer is not null)
        {
            ruleName = "Mise à jour de l'ordinateur (agent associé)";
            inputValue = $"agent #{agent.Id}";
        }
        // Fallback de corrélation : si l'agent n'est pas encore lié, on tente de retrouver
        // le poste par son UUID matériel (stable même si le nom de machine change).
        else if (content.Hardware?.Uuid is { Length: > 0 } uuid && (computer = await WithInventoryCollections(db.Computers)
                     .FirstOrDefaultAsync(c => c.HardwareUuid == uuid, cancellationToken)) is not null)
        {
            ruleName = "Mise à jour de l'ordinateur (par UUID matériel)";
            inputValue = uuid;
        }
        else
        {
            computer = new Computer
            {
                Name = content.Hardware?.Name ?? agent.Hostname ?? agent.DeviceId ?? "Inconnu",
                EntityId = ResolveEntityId(agent, content),
            };

            // Unicité des champs (voir FieldUnicityService) : contrôlée ici seulement, c'est-à-dire
            // quand l'inventaire ne correspond à aucun poste connu. Une remontée d'un poste déjà
            // enregistré n'est pas un doublon, et la contrôler ferait refuser chaque inventaire
            // suivant d'un poste dont les valeurs viennent justement d'être enregistrées.
            //
            // Le poste candidat est renseigné avant tout écrit en base — ApplyHardware ne touche que
            // l'objet qu'on lui passe — pour que le contrôle porte sur les valeurs réellement
            // enregistrées plutôt que sur le seul nom.
            ApplyHardware(computer, content, dictionaries, blacklist, EmptyLockedFields);

            FieldUnicityVerdict verdict = await fieldUnicity.CheckAsync(
                ItemTypes.Computer, db.Computers.AsNoTracking(), computer, cancellationToken: cancellationToken);

            if (verdict.Refused)
            {
                db.Set<RefusedImportLog>().Add(new RefusedImportLog
                {
                    RuleName = $"Unicité des champs — {verdict.CriterionName}",
                    ComputerName = importContext.ComputerName,
                    SerialNumber = importContext.SerialNumber,
                    Domain = importContext.Domain,
                    Tag = importContext.Tag,
                    IpAddress = importContext.IpAddresses.FirstOrDefault(ip => !string.IsNullOrWhiteSpace(ip)),
                    AgentIdentifier = agent.DeviceId ?? agent.Hostname ?? agent.AgentUuid
                });

                await db.SaveChangesAsync(cancellationToken);
                return null;
            }

            db.Computers.Add(computer);
            isNew = true;

            ruleName = "Création de l'ordinateur";
            inputValue = content.Hardware?.Uuid ?? agent.DeviceId;
        }

        // Actions de la règle d'affectation à l'import correspondante (le cas échéant) : appliquées
        // ici, avant Apply* ci-dessous, pour que les règles métier pour les actifs (ComputerRuleEngine,
        // plus bas) restent la dernière étape à pouvoir modifier l'ordinateur — même ordre que GLPI.
        if (importResult.LocationName is { Length: > 0 } locationName)
        {
            computer.LocationId = await ResolveLocationIdAsync(locationName, cancellationToken);
        }
        if (importResult.Technician is { Length: > 0 } technician)
        {
            computer.AssignedUser = technician;
        }

        // Rattrapage sur un poste déjà connu : un TAG posé après coup (ou une entité créée depuis)
        // doit finir par ranger le poste au bon endroit. Uniquement s'il est encore dans l'entité
        // racine, c'est-à-dire là où l'import le dépose faute de mieux — un rattachement décidé par
        // un administrateur n'est jamais écrasé.
        if (!isNew && computer.EntityId == entityTree.GetRootEntityId()
            && ResolveEntityId(agent, content) is { } resolvedEntityId
            && resolvedEntityId != computer.EntityId)
        {
            computer.EntityId = resolvedEntityId;
        }

        ComputerSnapshot before = ComputerSnapshot.Capture(computer);

        // Verrous du poste : chargés avant toute écriture.
        IReadOnlySet<string> lockedFields = computer.Id > 0
            ? (await db.Set<LockedField>()
                .AsNoTracking()
                .Where(lockedField => lockedField.ItemType == ComputerLockableFields.ItemType && lockedField.ItemId == computer.Id)
                .Select(lockedField => lockedField.Field)
                .ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal)
            : EmptyLockedFields;

        ApplyHardware(computer, content, dictionaries, blacklist, lockedFields);
        ApplyComponents(computer, content, settings);
        if (settings.ImportSoftwares) ApplySoftwares(computer, content, dictionaries);
        if (settings.ImportMonitors) ApplyMonitors(computer, content);
        if (settings.ImportPeripherals) await ApplyPeripheralsAsync(computer, content, cancellationToken);
        ApplyVolumes(computer, content, settings);
        if (settings.ImportBatteries) ApplyBatteries(computer, content);

        // Catalogue de composants (Configuration > Composants) : les modèles rencontrés y sont
        // référencés au passage. Sans cela la page reste vide à jamais — les composants d'un
        // inventaire sont rattachés au poste, et rien ne tenait la liste des modèles connus du
        // parc, alors que c'est précisément ce que cette page prétend montrer.
        await CatalogComponentModelsAsync(computer, cancellationToken);
        ApplyConnectors(computer, content);
        ApplyNetworkPorts(computer, content, blacklist);
        if (settings.ImportAntivirus) ApplyAntivirus(computer, content);
        await ApplySimCardsAsync(computer, content, cancellationToken);

        // Règles métier pour les actifs (voir ComputerRuleEngine) : appliquées après tous les
        // champs ci-dessus, une fois l'ordinateur entièrement renseigné par l'inventaire.
        List<ComputerRule> computerRules = await LoadActiveComputerRulesAsync(cancellationToken);

        // Statuts pré-résolus pour les actions qui en affectent un : le moteur est synchrone et
        // n'accède pas à la base. Ici, contrairement à la tâche périodique, un statut inconnu est
        // créé à la volée — l'import crée déjà les intitulés dont il a besoin (voir
        // ResolveStatusIdAsync), et une règle d'inventaire est jouée à chaque remontée.
        Dictionary<string, int> ruleStatusIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string statusName in computerRules
                     .SelectMany(rule => rule.Actions)
                     .Where(action => action.Field == "Status" && !string.IsNullOrWhiteSpace(action.Value))
                     .Select(action => action.Value!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (await ResolveStatusIdAsync(statusName, cancellationToken) is { } statusId)
            {
                ruleStatusIds[statusName] = statusId;
            }
        }

        // Statut par défaut : à la création seulement, et avant les règles pour qu'une règle « à
        // l'ajout » puisse le remplacer. Il était jusqu'ici affecté en dur à chaque inventaire,
        // après les règles : un poste passé manuellement en maintenance repassait « En
        // production » à sa remontée suivante, et une règle sur le statut était systématiquement
        // écrasée.
        if (isNew && settings.DefaultComputerStatus is { Length: > 0 } defaultStatusName)
        {
            computer.StatusId = await ResolveStatusIdAsync(defaultStatusName, cancellationToken);
        }

        ComputerRuleEngine.Apply(computer, isNew, computerRules,
            statusName => ruleStatusIds.TryGetValue(statusName, out int id) ? id : null);

        // Un poste à la corbeille qui se remet à envoyer des inventaires est un poste revenu :
        // le laisser à la corbeille en le mettant à jour dans l'ombre donnait un poste vivant mais
        // introuvable, que rien dans l'application ne permettait de faire réapparaître. Même
        // comportement que GLPI, où un inventaire restaure un actif supprimé.
        bool restoredFromTrash = computer.IsDeleted;
        if (restoredFromTrash)
        {
            computer.IsDeleted = false;
        }

        computer.LastInventoryAt = DateTime.UtcNow;

        // Libellés lus après coup, depuis les identifiants réellement retenus : le statut final
        // peut venir du défaut, d'une règle, ou d'aucun des deux. Une constante ne pouvait le
        // décrire fidèlement.
        string? beforeStatusLabel = await ResolveStatusLabelAsync(before.StatusId, cancellationToken);
        string? afterStatusLabel = await ResolveStatusLabelAsync(computer.StatusId, cancellationToken);

        computer.ImportHistories.Add(new ComputerImportHistory
        {
            OccurredAt = DateTime.UtcNow,
            RuleName = ruleName,
            Module = "Inventaire",
            AgentIdentifier = agent.DeviceId ?? agent.Hostname ?? agent.AgentUuid,
            InputValue = inputValue
        });

        foreach (ComputerHistoryEntry entry in BuildHistoryEntries(before, computer, isNew, beforeStatusLabel, afterStatusLabel))
        {
            computer.HistoryEntries.Add(entry);
        }

        if (restoredFromTrash)
        {
            computer.HistoryEntries.Add(new ComputerHistoryEntry
            {
                User = "Inventaire",
                Field = "Corbeille",
                Description = "Poste sorti de la corbeille : son agent a de nouveau remonté un inventaire.",
            });
        }

        agent.Computer = computer;

        await db.SaveChangesAsync(cancellationToken);

        if (isNew)
        {
            await PublishNewComputerNotificationAsync(computer, cancellationToken);
        }

        // Règles de déploiement (voir DeploymentRuleEngine, module Deployment) : réévaluées à
        // chaque inventaire, une fois l'ordinateur enregistré (computer.Id/agent.Id garantis
        // renseignés). Assigne automatiquement les paquets correspondants, sans dupliquer ceux
        // déjà assignés à cet agent (par cette règle ou manuellement).
        await ApplyDeploymentRulesAsync(computer, agent.Id, cancellationToken);

        return computer;
    }

    /// <summary>
    /// Charge un ordinateur avec toutes les collections que l'import réécrit.
    ///
    /// <b>AsSplitQuery est ce qui rend cette requête viable</b>, pas une optimisation de confort.
    /// Sept <c>Include</c> de collections dans une requête unique font produire à EF Core un seul
    /// SQL où les sept tables sont jointes entre elles : le résultat n'est pas la somme des lignes
    /// mais leur <i>produit</i>. Sur un poste de bureau ordinaire — 840 logiciels, une centaine de
    /// composants, une quinzaine de périphériques, quelques volumes et ports — cela se compte en
    /// centaines de millions de lignes, chacune portant les colonnes des sept tables. La requête
    /// ne revient jamais, et l'agent abandonne au bout de son délai de lecture de trois minutes en
    /// signalant un « 500 read timeout » qui n'a pourtant jamais été renvoyé par le serveur.
    ///
    /// En mode fractionné, EF émet une requête par collection : sept requêtes qui rendent chacune
    /// ses quelques centaines de lignes.
    /// </summary>
    private static IQueryable<Computer> WithInventoryCollections(IQueryable<Computer> computers)
        => computers
            .Include(c => c.Components)
            .Include(c => c.Softwares)
            .Include(c => c.Peripherals)
            .Include(c => c.Volumes)
            .Include(c => c.Batteries)
            .Include(c => c.NetworkPorts)
            .Include(c => c.Antiviruses)
            .AsSplitQuery();

    private async Task ApplyDeploymentRulesAsync(Computer computer, int agentId, CancellationToken cancellationToken)
    {
        // Chargée après SaveChangesAsync : StatusId vient d'être résolu ci-dessus, mais l'affecter
        // ne peuple pas automatiquement la navigation StatusItem qu'évalue un critère "Statut".
        await db.Entry(computer).Reference(c => c.StatusItem).LoadAsync(cancellationToken);

        List<DeploymentRule> rules = await db.Set<DeploymentRule>()
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Include(r => r.Actions)
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        List<int> newPackageIds = await DeploymentRuleEngine.ResolveNewPackageIdsAsync(db, computer, agentId, rules, cancellationToken);
        if (newPackageIds.Count == 0)
        {
            return;
        }

        await deploymentAssignmentService.AssignPackagesAsync(computer.Id, newPackageIds, cancellationToken);
    }

    /// <summary>Déclenche l'événement "Nouvel ordinateur découvert" (voir NotificationEventCatalog.InventoryComputer) après la création effective en base.</summary>
    private async Task PublishNewComputerNotificationAsync(Computer computer, CancellationToken cancellationToken)
    {
        string? baseUrl = BuildBaseUrl();

        Dictionary<string, string?> variables = new()
        {
            ["computer.name"] = computer.Name,
            ["computer.serial"] = computer.SerialNumber,
            ["computer.manufacturer"] = computer.Manufacturer,
            ["computer.model"] = computer.Model,
            ["computer.os"] = computer.OperatingSystem,
            ["computer.url"] = baseUrl is null ? null : $"{baseUrl}/parc/computer/{computer.Id}",
        };

        await notificationDispatch.PublishAsync(
            NotificationEventCatalog.InventoryComputer, NotificationEventCatalog.EventNew, computer.Id, variables, cancellationToken);
    }

    /// <summary>
    /// Reconstruit l'origine (schéma+hôte) de la requête HTTP courante pour les balises ##...url##
    /// des notifications. Null hors contexte de requête (ex. import depuis un fichier via une tâche
    /// arrière-plan) : le lien est alors simplement omis plutôt que de pointer vers une URL fausse.
    /// </summary>
    private string? BuildBaseUrl()
    {
        HttpRequest? request = httpContextAccessor.HttpContext?.Request;
        return request is null ? null : $"{request.Scheme}://{request.Host}";
    }

    /// <summary>
    /// Utilisé par l'import manuel de fichier (onglet "Importer depuis un fichier" de
    /// /admin/inventory) : contrairement à AgentController.HandleInventoryAsync, il n'y
    /// a pas d'en-tête GLPI-Agent-ID côté fichier, donc le "deviceid" du contenu importé sert
    /// directement d'identité d'agent stable pour retrouver/créer l'ordinateur correspondant.
    /// </summary>
    public async Task<Computer?> ImportFromDeviceIdAsync(string deviceId, InventoryContent content, CancellationToken cancellationToken = default)
    {
        GlpiAgent? agent = await db.Agents.FirstOrDefaultAsync(a => a.AgentUuid == deviceId, cancellationToken);
        if (agent is null)
        {
            agent = new GlpiAgent { AgentUuid = deviceId, DeviceId = deviceId };
            db.Agents.Add(agent);
        }

        return await ImportAsync(agent, content, cancellationToken);
    }

    // « locked » : champs verrouillés sur ce poste (voir LockedField). L'inventaire ne les écrit
    // pas — c'est le seul moyen de conserver une valeur corrigée à la main, qu'une remontée d'agent
    // réécrirait sinon quelques heures plus tard.
    private static void ApplyHardware(
        Computer computer,
        InventoryContent content,
        IReadOnlyDictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries,
        IReadOnlyDictionary<ImportBlacklistType, HashSet<string>> blacklist,
        IReadOnlySet<string> locked)
    {
        if (content.Hardware is { } hardware)
        {
            if (!locked.Contains("Name")) computer.Name = string.IsNullOrWhiteSpace(hardware.Name) ? computer.Name : hardware.Name;
            if (!locked.Contains("HardwareUuid")) computer.HardwareUuid = hardware.Uuid ?? computer.HardwareUuid;
            if (!locked.Contains("ChassisType")) computer.ChassisType = hardware.ChassisType ?? computer.ChassisType;
            if (!locked.Contains("TotalMemoryMb")) computer.TotalMemoryMb = hardware.MemoryMb ?? computer.TotalMemoryMb;
            if (!locked.Contains("LastLoggedUser")) computer.LastLoggedUser = hardware.LastLoggedUser ?? computer.LastLoggedUser;
            if (!locked.Contains("VmSystem")) computer.VmSystem = hardware.VmSystem ?? computer.VmSystem;
            if (!locked.Contains("Domain")) computer.Domain = hardware.Workgroup ?? computer.Domain;
        }

        // Prise en main à distance : l'agent peut en remonter plusieurs (TeamViewer et AnyDesk
        // installés côte à côte). On garde la première entrée exploitable — le modèle n'en porte
        // qu'une, et c'est l'information « comment joindre ce poste » qui compte, pas l'inventaire
        // exhaustif des outils installés (les logiciels s'en chargent).
        if (!locked.Contains("RemoteManagement")
            && content.RemoteManagement.FirstOrDefault(remote => !string.IsNullOrWhiteSpace(remote.Id)) is { } remoteManagement)
        {
            computer.RemoteManagementId = remoteManagement.Id;
            computer.RemoteManagementType = remoteManagement.Type;
        }

        if (content.Bios is { } bios)
        {
            DictionaryRuleEngine.Result manufacturer = DictionaryRuleEngine.Apply(bios.SystemManufacturer, GetRules(dictionaries, DictionaryRuleType.Manufacturer));
            if (!manufacturer.Ignore && !locked.Contains("Manufacturer")) computer.Manufacturer = manufacturer.Value ?? computer.Manufacturer;

            DictionaryRuleEngine.Result model = DictionaryRuleEngine.Apply(bios.SystemModel, GetRules(dictionaries, DictionaryRuleType.ComputerModel));
            if (!model.Ignore && !locked.Contains("Model")) computer.Model = model.Value ?? computer.Model;

            // Liste noire (voir ImportBlacklistEngine) : un numéro de série placeholder du
            // constructeur ("SYS-1234567890", etc.) est traité comme absent plutôt qu'enregistré.
            string? serial = ImportBlacklistEngine.IsBlacklisted(blacklist, ImportBlacklistType.SerialNumber, bios.SystemSerial) ? null : bios.SystemSerial;
            if (!locked.Contains("SerialNumber")) computer.SerialNumber = serial ?? computer.SerialNumber;
        }

        if (content.OperatingSystem is { } os)
        {
            DictionaryRuleEngine.Result osName = DictionaryRuleEngine.Apply(os.FullName ?? os.Name, GetRules(dictionaries, DictionaryRuleType.OperatingSystem));
            if (!osName.Ignore && !locked.Contains("OperatingSystem")) computer.OperatingSystem = osName.Value ?? computer.OperatingSystem;

            DictionaryRuleEngine.Result osVersion = DictionaryRuleEngine.Apply(os.Version, GetRules(dictionaries, DictionaryRuleType.OperatingSystemVersion));
            if (!osVersion.Ignore && !locked.Contains("OsVersion")) computer.OsVersion = osVersion.Value ?? computer.OsVersion;

            if (!locked.Contains("OsKernelVersion")) computer.OsKernelVersion = os.KernelVersion ?? computer.OsKernelVersion;
        }
    }

    private static readonly List<DictionaryRule> EmptyDictionaryRules = [];

    private static List<DictionaryRule> GetRules(IReadOnlyDictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries, DictionaryRuleType type) =>
        dictionaries.TryGetValue(type, out List<DictionaryRule>? rules) ? rules : EmptyDictionaryRules;

    private async Task<Dictionary<DictionaryRuleType, List<DictionaryRule>>> LoadActiveDictionaryRulesAsync(CancellationToken cancellationToken)
    {
        List<DictionaryRule> rules = await db.Set<DictionaryRule>()
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

        return rules.GroupBy(r => r.Type).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Voir ComputerRuleEngine.Apply, invoqué depuis ImportAsync une fois tous les champs de l'ordinateur renseignés.</summary>
    private async Task<List<ComputerRule>> LoadActiveComputerRulesAsync(CancellationToken cancellationToken) =>
        await db.Set<ComputerRule>()
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Include(r => r.Actions)
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

    /// <summary>Voir ImportAssignmentRuleEngine.Evaluate, invoqué depuis ImportAsync avant toute résolution du Computer.</summary>
    private async Task<List<ImportAssignmentRule>> LoadActiveImportAssignmentRulesAsync(CancellationToken cancellationToken) =>
        await db.Set<ImportAssignmentRule>()
            .AsNoTracking()
            .Include(r => r.Criteria)
            .Include(r => r.Actions)
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(cancellationToken);

    private async Task<Dictionary<ImportBlacklistType, HashSet<string>>> LoadBlacklistAsync(CancellationToken cancellationToken)
    {
        List<ImportBlacklistEntry> entries = await db.Set<ImportBlacklistEntry>().AsNoTracking().ToListAsync(cancellationToken);
        return ImportBlacklistEngine.Index(entries);
    }

    /// <summary>
    /// Entité de rattachement d'un poste inventorié. Un inventaire arrive hors session applicative,
    /// il n'y a donc pas d'entité active à reprendre (voir
    /// <c>GlpiNgDbContext.StampActiveEntityOnNewEntries</c>), et un poste non rattaché resterait
    /// visible de toutes les entités.
    ///
    /// Le TAG de l'agent est confronté au TAG d'affectation des entités (onglet « Informations
    /// avancées » de la fiche Entité) — équivalent réduit de RuleImportEntity côté GLPI. Le TAG
    /// remonté dans le corps de l'inventaire (section <c>accountinfo</c>) prime sur celui retenu du
    /// dernier contact de l'agent : c'est le plus frais des deux.
    ///
    /// Sans correspondance, le poste va dans l'entité racine, comme l'existant repris par la
    /// migration AddEntityScoping, à charge pour un administrateur de le réaffecter.
    /// </summary>
    private int? ResolveEntityId(GlpiAgent agent, InventoryContent content)
        => entityTree.GetEntityIdByAssignmentTag(content.Tag ?? agent.Tag) ?? entityTree.GetRootEntityId();

    /// <summary>
    /// Construit le contexte évalué par ImportAssignmentRuleEngine à partir des seules données
    /// brutes de la requête (avant toute résolution/création du Computer) : nom, série et domaine
    /// depuis le contenu de l'inventaire, tag depuis l'agent (renseigné par un contact préalable,
    /// voir AgentController), adresses IP de toutes les interfaces réseau remontées.
    /// </summary>
    private static ImportAssignmentRuleContext BuildImportAssignmentContext(GlpiAgent agent, InventoryContent content) => new(
        ComputerName: content.Hardware?.Name,
        SerialNumber: content.Bios?.SystemSerial,
        Domain: content.Hardware?.Workgroup,
        Tag: content.Tag ?? agent.Tag,
        IpAddresses: content.Networks.Select(n => n.IpAddress).Where(ip => !string.IsNullOrWhiteSpace(ip)).ToList());

    // Résout (ou crée à la volée) l'Id du DropdownItem de type Location portant ce nom — même
    // principe que ResolveStatusIdAsync ci-dessous, pour l'action AssignLocation d'une
    // ImportAssignmentRule.
    private async Task<int?> ResolveLocationIdAsync(string name, CancellationToken cancellationToken)
    {
        DropdownItem? item = await db.DropdownItems
            .FirstOrDefaultAsync(i => i.Type == DropdownType.Location && i.Name == name, cancellationToken);
        if (item is not null) return item.Id;

        item = new DropdownItem { Type = DropdownType.Location, Name = name };
        db.DropdownItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    /// <summary>
    /// Remplace intégralement les composants du poste par ceux de l'inventaire courant.
    /// Choix volontaire : un inventaire GLPI-Agent est toujours un instantané complet,
    /// donc "remplacer" reflète mieux la réalité qu'un merge incrémental fragile.
    /// </summary>
    private static void ApplyComponents(Computer computer, InventoryContent content, InventorySettings settings)
    {
        computer.Components.Clear();

        if (settings.ImportCpus)
        {
            foreach (InventoryCpu cpu in content.Cpus)
            {
                computer.Components.Add(new ComputerComponent
                {
                    Type = ComponentType.Cpu,
                    Designation = cpu.Name ?? cpu.Manufacturer ?? "CPU inconnu",
                    Capacity = cpu.SpeedMhz is { } mhz ? $"{mhz} MHz / {cpu.Cores ?? 0} coeurs" : null,
                    Serial = cpu.Serial
                });
            }
        }

        if (settings.ImportMemories)
        {
            foreach (InventoryMemory memory in content.Memories)
            {
                // Un slot vide (pas de capacité) n'est pas un module physique installé — on l'ignore.
                if (memory.CapacityMb is null or 0) continue;

                computer.Components.Add(new ComputerComponent
                {
                    Type = ComponentType.Ram,
                    Designation = memory.Description ?? memory.Caption ?? "Barrette mémoire",
                    Capacity = $"{memory.CapacityMb} Mo",
                    Serial = memory.Serial
                });
            }
        }

        if (settings.ImportDisks)
        {
            foreach (InventoryStorage storage in content.Storages)
            {
                computer.Components.Add(new ComputerComponent
                {
                    Type = ComponentType.Disk,
                    Designation = storage.Model ?? storage.Name ?? "Disque inconnu",
                    Capacity = storage.DiskSizeMb is { } mb ? $"{mb} Mo" : null,
                    Serial = storage.Serial
                });
            }
        }

        if (settings.ImportNetworkCards)
        {
            foreach (InventoryNetwork network in content.Networks)
            {
                computer.Components.Add(new ComputerComponent
                {
                    Type = ComponentType.NetworkCard,
                    Designation = network.Description ?? "Carte réseau",
                    Capacity = null,
                    Serial = network.MacAddress
                });
            }
        }

        // Cartes graphiques, contrôleurs, cartes son et modems : sections du protocole que GlpiNg
        // ne lisait pas, alors que ComponentType.Gpu existait déjà sans jamais être alimenté. Pas
        // de réglage dédié dans InventorySettings — ce sont des composants matériels au même titre
        // que les précédents, et en ajouter un par section multiplierait les cases pour rien.
        AddGenericComponents(computer, content.Videos, ComponentType.Gpu, "Carte graphique");
        AddGenericComponents(computer, content.Controllers, ComponentType.Controller, "Contrôleur");
        AddGenericComponents(computer, content.Sounds, ComponentType.SoundCard, "Carte son");
        AddGenericComponents(computer, content.Modems, ComponentType.Modem, "Modem");

        AddFirmware(computer, content);
    }

    /// <summary>
    /// Reprend le BIOS/UEFI de la section "bios" comme composant, à l'image du Firmware de GLPI.
    ///
    /// La section était déjà lue, mais seulement pour ses champs "s*" (smanufacturer, smodel, ssn),
    /// qui décrivent la machine — pas le firmware. Sa version et sa date, elles, n'étaient nulle
    /// part, alors qu'un parc se pilote en grande partie sur « qui n'est pas à jour ».
    ///
    /// Rien n'est ajouté si l'agent ne remonte ni version ni fabricant : un composant « BIOS » vide
    /// n'apprendrait rien et alourdirait l'onglet de chaque poste.
    /// </summary>
    private static void AddFirmware(Computer computer, InventoryContent content)
    {
        if (content.Bios is not { } bios)
        {
            return;
        }

        string? manufacturer = FirstNonBlank(bios.BiosManufacturer);
        string? version = FirstNonBlank(bios.BiosVersion);
        string? date = FirstNonBlank(bios.BiosDate);

        if (manufacturer is null && version is null)
        {
            return;
        }

        computer.Components.Add(new ComputerComponent
        {
            Type = ComponentType.Firmware,
            Designation = manufacturer ?? "BIOS",
            Capacity = version is null
                ? date
                : date is null ? version : $"{version} ({date})"
        });
    }

    /// <summary>
    /// Reprend les cartes SIM remontées par l'agent (section "simcards") dans les actifs Carte SIM.
    ///
    /// Corrélation par ICCID, qui est l'identifiant gravé sur la carte : une carte déplacée d'un
    /// poste à l'autre reste le même actif. Une carte inconnue est créée dans l'entité du poste ;
    /// une carte déjà connue voit ses seules informations réseau rafraîchies — pas son nom, son
    /// statut ni son lieu, qui relèvent de la gestion manuelle et ne doivent pas être écrasés à
    /// chaque inventaire.
    ///
    /// Les codes PIN/PUK ne sont jamais touchés : l'agent ne les remonte pas, et un champ vide ne
    /// doit pas effacer ce qu'un administrateur a saisi.
    /// </summary>
    private async Task ApplySimCardsAsync(Computer computer, InventoryContent content, CancellationToken cancellationToken)
    {
        foreach (InventorySimCard reported in content.SimCards)
        {
            if (string.IsNullOrWhiteSpace(reported.Iccid))
            {
                continue;
            }

            string iccid = reported.Iccid.Trim();
            SimCard? existing = await db.Set<SimCard>().FirstOrDefaultAsync(card => card.SerialNumber == iccid, cancellationToken);

            if (existing is null)
            {
                existing = new SimCard
                {
                    Name = iccid,
                    SerialNumber = iccid,
                    EntityId = computer.EntityId,
                };
                db.Set<SimCard>().Add(existing);
            }

            existing.Operator = reported.OperatorName ?? existing.Operator;
            existing.PhoneNumber = reported.PhoneNumber ?? existing.PhoneNumber;
            existing.Country = reported.Country ?? existing.Country;
            existing.Msin = reported.Imsi ?? existing.Msin;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Ajoute les composants d'une section à forme générique. La capacité ne vaut que pour les
    /// cartes graphiques, seule section à remonter une mémoire.
    /// </summary>
    private static void AddGenericComponents(Computer computer, List<InventoryGenericDevice> devices, ComponentType type, string fallbackDesignation)
    {
        foreach (InventoryGenericDevice device in devices)
        {
            computer.Components.Add(new ComputerComponent
            {
                Type = type,
                Designation = device.Designation ?? fallbackDesignation,
                Capacity = device.Memory is { } memoryMb and > 0 ? $"{memoryMb} Mo" : null,
                Serial = null,
            });
        }
    }

    /// <summary>
    /// Remplace intégralement les logiciels du poste par ceux de l'inventaire courant, pour
    /// la même raison que <see cref="ApplyComponents"/> : un inventaire GLPI-Agent est un
    /// instantané complet des logiciels installés.
    /// </summary>
    private static void ApplySoftwares(Computer computer, InventoryContent content, IReadOnlyDictionary<DictionaryRuleType, List<DictionaryRule>> dictionaries)
    {
        computer.Softwares.Clear();

        List<DictionaryRule> rules = GetRules(dictionaries, DictionaryRuleType.Software);

        foreach (InventorySoftware software in content.Softwares)
        {
            if (string.IsNullOrWhiteSpace(software.Name)) continue;

            DictionaryRuleEngine.Result result = DictionaryRuleEngine.Apply(software.Name, software.Publisher, rules);
            if (result.Ignore) continue;

            computer.Softwares.Add(new ComputerSoftware
            {
                Name = result.Value ?? software.Name,
                Version = software.Version,
                Publisher = software.Publisher,
                InstallDate = software.InstallDate
            });
        }
    }

    /// <summary>
    /// Remplace les moniteurs (<see cref="PeripheralKind.Monitor"/>) du poste par ceux de
    /// l'inventaire courant, même logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// Ne touche pas aux autres natures de <see cref="ComputerPeripheral"/> (imprimantes, etc.),
    /// qui ne sont pas encore alimentées par l'inventaire.
    /// </summary>
    private static void ApplyMonitors(Computer computer, InventoryContent content)
    {
        computer.Peripherals.RemoveAll(peripheral => peripheral.Kind == PeripheralKind.Monitor);

        foreach (InventoryMonitor monitor in content.Monitors)
        {
            string designation = monitor.Caption ?? monitor.Description ?? "Écran inconnu";

            computer.Peripherals.Add(new ComputerPeripheral
            {
                Kind = PeripheralKind.Monitor,
                Designation = designation,
                Manufacturer = monitor.Manufacturer,
                Serial = monitor.Serial
            });
        }
    }

    /// <summary>
    /// Crée ou met à jour les entités <see cref="Peripheral"/> autonomes à partir des
    /// périphériques USB et d'entrée remontés par l'agent, et les rattache à l'ordinateur.
    /// La corrélation se fait par numéro de série (quand disponible), sinon par
    /// nom+fabricant sur le même ordinateur, pour éviter les doublons à chaque inventaire.
    /// </summary>
    private async Task ApplyPeripheralsAsync(Computer computer, InventoryContent content, CancellationToken cancellationToken)
    {
        // Un ordinateur qui vient d'être créé n'a pas encore d'identifiant : inutile d'interroger
        // la base, et surtout la comparaison porterait sur ComputerId == 0, qui ne désigne rien.
        List<Peripheral> existingPeripherals = computer.Id > 0
            ? await db.Set<Peripheral>().Where(p => p.ComputerId == computer.Id).ToListAsync(cancellationToken)
            : [];

        HashSet<int> seenIds = [];

        foreach (InventoryUsbDevice usb in content.UsbDevices)
        {
            string name = usb.Name ?? "Périphérique USB inconnu";
            if (string.IsNullOrWhiteSpace(name) || name == "usb") continue;

            string type = ResolveUsbType(usb);

            Peripheral? peripheral = FindExistingPeripheral(existingPeripherals, name, usb.Manufacturer, usb.Serial);

            if (peripheral is null)
            {
                peripheral = new Peripheral { Name = name };
                db.Set<Peripheral>().Add(peripheral);
                existingPeripherals.Add(peripheral);
            }

            peripheral.Manufacturer = usb.Manufacturer ?? peripheral.Manufacturer;
            peripheral.SerialNumber = usb.Serial ?? peripheral.SerialNumber;
            peripheral.Type = type;
            // Rattachement par la navigation et non par la clé étrangère : au premier inventaire,
            // l'ordinateur n'est pas encore inséré et son Id vaut 0. Écrire ComputerId = 0
            // envoyait les périphériques dans le même lot que l'INSERT du Computer, avec une
            // valeur qui ne référence aucune ligne — d'où « The MERGE statement conflicted with
            // the FOREIGN KEY constraint FK_Peripherals_Computers_ComputerId » et l'échec de tout
            // l'inventaire. Par la navigation, EF ordonne les écritures et reporte l'Id généré.
            peripheral.Computer = computer;
            peripheral.StatusId = await ResolveStatusIdAsync("En production", cancellationToken);
            peripheral.UpdatedAt = DateTime.UtcNow;

            if (peripheral.Id > 0) seenIds.Add(peripheral.Id);
        }

        foreach (InventoryInput input in content.Inputs)
        {
            string name = input.Name ?? input.Caption ?? input.Description ?? "Périphérique d'entrée inconnu";
            if (string.IsNullOrWhiteSpace(name)) continue;

            string type = ResolveInputType(input);

            Peripheral? peripheral = FindExistingPeripheral(existingPeripherals, name, input.Manufacturer, serial: null);

            if (peripheral is null)
            {
                peripheral = new Peripheral { Name = name };
                db.Set<Peripheral>().Add(peripheral);
                existingPeripherals.Add(peripheral);
            }

            peripheral.Manufacturer = input.Manufacturer ?? peripheral.Manufacturer;
            peripheral.Type = type;
            // Voir la remarque sur le rattachement par navigation plus haut.
            peripheral.Computer = computer;
            peripheral.StatusId = await ResolveStatusIdAsync("En production", cancellationToken);
            peripheral.UpdatedAt = DateTime.UtcNow;

            if (peripheral.Id > 0) seenIds.Add(peripheral.Id);
        }
    }

    /// <summary>Nom de l'intitulé de statut, pour l'historique. Ne crée rien, contrairement à ResolveStatusIdAsync.</summary>
    private async Task<string?> ResolveStatusLabelAsync(int? statusId, CancellationToken cancellationToken)
        => statusId is { } id
            ? await db.DropdownItems.AsNoTracking().Where(i => i.Id == id).Select(i => i.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

    // Résout (ou crée à la volée) l'Id du DropdownItem de type Status portant ce nom — voir
    // Models/DropdownItem.cs. Utilisé pour tous les statuts assignés automatiquement par cet
    // import (toujours "En production", que ce soit pour un Computer ou un Peripheral détecté),
    // plutôt que l'ancienne énumération fixe ComputerStatus.
    /// <summary>
    /// Référence dans le catalogue les modèles de composants que ce poste vient de remonter.
    ///
    /// Le catalogue est une liste de modèles connus du parc, pas un inventaire par machine : une
    /// entrée par nom distinct, quel que soit le nombre de postes qui la portent. Les doublons
    /// sont écartés par une lecture préalable des noms déjà connus pour les seules catégories
    /// concernées — une requête par catégorie présente dans cet inventaire, pas une par composant.
    ///
    /// Les désignations vides ou manifestement inutiles (« inconnu », « CPU inconnu ») ne sont pas
    /// cataloguées : elles ne désignent aucun modèle et pollueraient une liste dont l'intérêt est
    /// justement d'être parcourable.
    /// </summary>
    private async Task CatalogComponentModelsAsync(Computer computer, CancellationToken cancellationToken)
    {
        Dictionary<DropdownType, HashSet<string>> wanted = [];

        void Want(DropdownType? type, string? name)
        {
            if (type is not { } catalogType || !DropdownTypeCatalog.IsCatalogueableName(name))
            {
                return;
            }

            if (!wanted.TryGetValue(catalogType, out HashSet<string>? names))
            {
                names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                wanted[catalogType] = names;
            }

            names.Add(name!.Trim());
        }

        foreach (ComputerComponent component in computer.Components)
        {
            Want(DropdownTypeCatalog.ForComponent(component.Type), component.Designation);
        }

        foreach (ComputerBattery battery in computer.Batteries)
        {
            Want(DropdownType.Battery, battery.Name);
        }

        if (wanted.Count == 0)
        {
            return;
        }

        foreach ((DropdownType type, HashSet<string> names) in wanted)
        {
            List<string> known = await db.DropdownItems
                .Where(item => item.Type == type)
                .Select(item => item.Name)
                .ToListAsync(cancellationToken);

            HashSet<string> existing = new(known, StringComparer.OrdinalIgnoreCase);

            foreach (string name in names.Where(candidate => !existing.Contains(candidate)))
            {
                db.DropdownItems.Add(new DropdownItem { Type = type, Name = name });
            }
        }
    }

    private async Task<int?> ResolveStatusIdAsync(string name, CancellationToken cancellationToken)
    {
        DropdownItem? item = await db.DropdownItems
            .FirstOrDefaultAsync(i => i.Type == DropdownType.Status && i.Name == name, cancellationToken);
        if (item is not null) return item.Id;

        item = new DropdownItem { Type = DropdownType.Status, Name = name };
        db.DropdownItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    private static Peripheral? FindExistingPeripheral(List<Peripheral> existingPeripherals, string name, string? manufacturer, string? serial)
    {
        if (!string.IsNullOrWhiteSpace(serial))
        {
            Peripheral? bySerial = existingPeripherals.FirstOrDefault(p =>
                string.Equals(p.SerialNumber, serial, StringComparison.OrdinalIgnoreCase));
            if (bySerial is not null) return bySerial;
        }

        return existingPeripherals.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Manufacturer, manufacturer, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveUsbType(InventoryUsbDevice usb)
    {
        if (usb.Class is "03" or "3") return "HID";
        if (usb.Class is "08" or "8") return "Stockage USB";
        if (usb.Class is "09" or "9") return "Hub USB";
        if (usb.Class is "0e" or "14" or "0E") return "Webcam";
        if (usb.Class is "01" or "1") return "Audio USB";
        if (usb.Class is "07" or "7") return "Imprimante USB";
        return "Périphérique USB";
    }

    private static string ResolveInputType(InventoryInput input)
    {
        if (input.PointingType is not null) return "Dispositif de pointage";
        if (input.Type?.Contains("keyboard", StringComparison.OrdinalIgnoreCase) == true
            || input.Layout is not null) return "Clavier";
        if (input.Type?.Contains("mouse", StringComparison.OrdinalIgnoreCase) == true
            || input.Type?.Contains("touchpad", StringComparison.OrdinalIgnoreCase) == true) return "Dispositif de pointage";
        return "Périphérique d'entrée";
    }

    /// <summary>
    /// Remplace intégralement les volumes du poste par ceux de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// </summary>
    private static void ApplyVolumes(Computer computer, InventoryContent content, InventorySettings settings)
    {
        computer.Volumes.Clear();

        foreach (InventoryDrive drive in content.Drives)
        {
            if (!ShouldImportDrive(drive, settings)) continue;

            string name = drive.Volume ?? drive.Label ?? drive.Letter ?? "Volume inconnu";

            computer.Volumes.Add(new ComputerVolume
            {
                Name = name,
                Partition = drive.Type,
                MountPoint = drive.Letter ?? drive.Volume,
                FileSystem = drive.FileSystem,
                TotalSizeMb = drive.TotalMb,
                FreeSizeMb = drive.FreeMb
            });
        }
    }

    /// <summary>
    /// L'onglet Configuration de l'inventaire distingue "Volumes"/"Lecteurs réseaux"/"Lecteurs
    /// amovibles" comme dans GLPI. L'agent Windows remonte "type" comme le code DriveType de
    /// Win32_LogicalDisk (2 = amovible, 4 = réseau) ; les autres valeurs (3 = disque local, ou
    /// absentes sur les agents non-Windows) sont traitées comme des volumes classiques.
    /// </summary>
    private static bool ShouldImportDrive(InventoryDrive drive, InventorySettings settings) => drive.Type switch
    {
        "2" => settings.ImportRemovableDrives,
        "4" => settings.ImportNetworkDrives,
        _ => settings.ImportVolumes
    };

    /// <summary>
    /// Remplace intégralement les batteries du poste par celles de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// </summary>
    private static void ApplyBatteries(Computer computer, InventoryContent content)
    {
        computer.Batteries.Clear();

        foreach (InventoryBattery battery in content.Batteries)
        {
            computer.Batteries.Add(new ComputerBattery
            {
                Name = battery.Name ?? battery.Manufacturer ?? "Batterie inconnue",
                Manufacturer = battery.Manufacturer,
                Serial = battery.Serial,
                Chemistry = battery.Chemistry,
                VoltageMv = battery.VoltageMv,
                CapacityMwh = battery.CapacityMwh,
                RealCapacityMwh = battery.RealCapacityMwh,
                ManufactureDate = battery.ManufactureDate
            });
        }
    }

    /// <summary>
    /// Remplace intégralement les connecteurs du poste par ceux de l'inventaire courant (section
    /// "ports"), même logique d'instantané complet que <see cref="ApplyComponents"/>.
    ///
    /// Pas de réglage dédié dans <see cref="InventorySettings"/>, pour la même raison que les
    /// sections génériques d'<see cref="ApplyComponents"/> : une case par section multiplierait les
    /// réglages sans rien apporter.
    ///
    /// Les entrées sans aucun libellé exploitable sont ignorées : l'agent remonte volontiers des
    /// connecteurs vides pour des emplacements que le BIOS déclare sans les nommer, et une liste de
    /// « Connecteur » anonymes n'apprendrait rien.
    /// </summary>
    private static void ApplyConnectors(Computer computer, InventoryContent content)
    {
        computer.Connectors.Clear();

        foreach (InventoryPort port in content.Ports)
        {
            string? designation = FirstNonBlank(port.Name, port.Caption, port.Description);
            if (designation is null && string.IsNullOrWhiteSpace(port.Type))
            {
                continue;
            }

            computer.Connectors.Add(new ComputerConnector
            {
                Designation = designation ?? port.Type!,
                Type = port.Type,
                Caption = port.Caption,
                Description = port.Description
            });
        }
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    /// <summary>
    /// Remplace intégralement les ports réseau du poste par ceux de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>. Distinct de
    /// <see cref="ApplyComponents"/> (qui liste les cartes réseau comme composants matériel) :
    /// ici on conserve la configuration IP/logique de chaque interface, à l'instar de l'onglet
    /// "Ports réseau" de GLPI.
    /// </summary>
    private static void ApplyNetworkPorts(Computer computer, InventoryContent content, IReadOnlyDictionary<ImportBlacklistType, HashSet<string>> blacklist)
    {
        computer.NetworkPorts.Clear();

        foreach (InventoryNetwork network in content.Networks)
        {
            // Liste noire (voir ImportBlacklistEngine) : une IP/MAC placeholder connue
            // ("127.0.0.1", "00:00:00:00:00:00", etc.) est traitée comme absente sur cette interface.
            string? macAddress = ImportBlacklistEngine.IsBlacklisted(blacklist, ImportBlacklistType.MacAddress, network.MacAddress) ? null : network.MacAddress;
            string? ipAddress = ImportBlacklistEngine.IsBlacklisted(blacklist, ImportBlacklistType.IpAddress, network.IpAddress) ? null : network.IpAddress;

            computer.NetworkPorts.Add(new ComputerNetworkPort
            {
                Designation = network.Description ?? "Interface réseau",
                Type = network.Type,
                MacAddress = macAddress,
                Manufacturer = network.Manufacturer,
                IpAddress = ipAddress,
                IpMask = network.IpMask,
                IpGateway = network.IpGateway,
                IpSubnet = network.IpSubnet,
                IpDhcp = network.IpDhcp,
                Mtu = network.Mtu,
                SpeedMbps = network.SpeedMbps,
                Status = network.Status,
                IsVirtual = network.VirtualDevice == "1"
            });
        }
    }

    /// <summary>
    /// Remplace intégralement les antivirus du poste par ceux de l'inventaire courant, même
    /// logique d'instantané complet que <see cref="ApplyComponents"/>.
    /// </summary>
    private static void ApplyAntivirus(Computer computer, InventoryContent content)
    {
        computer.Antiviruses.Clear();

        foreach (InventoryAntivirus antivirus in content.Antivirus)
        {
            if (string.IsNullOrWhiteSpace(antivirus.Name)) continue;

            computer.Antiviruses.Add(new ComputerAntivirus
            {
                Name = antivirus.Name,
                Company = antivirus.Company,
                Guid = antivirus.Guid,
                Version = antivirus.Version,
                Enabled = antivirus.Enabled,
                UpToDate = antivirus.UpToDate,
                Expiration = antivirus.Expiration,
                BaseCreationDate = antivirus.BaseCreationDate,
                BaseVersion = antivirus.BaseVersion
            });
        }
    }

    /// <summary>
    /// Photo de l'état d'un ordinateur juste avant application d'un inventaire, utilisée pour
    /// produire les entrées de l'onglet "Historique" en comparant avant/après. Les listes sont
    /// des clés textuelles (pas les entités elles-mêmes) car Apply* vide et recrée les collections
    /// à chaque import : comparer par identité EF ne distinguerait pas "inchangé" de "recréé".
    /// </summary>
    private sealed record ComputerSnapshot(
        string? Name, string? SerialNumber, string? Manufacturer, string? Model,
        string? OperatingSystem, string? OsVersion, string? OsKernelVersion,
        string? HardwareUuid, string? ChassisType, int? TotalMemoryMb,
        string? LastLoggedUser, string? VmSystem, string? Domain, int? StatusId, DateTime? LastInventoryAt,
        HashSet<string> Components, HashSet<string> Softwares, HashSet<string> Monitors,
        HashSet<string> Volumes, HashSet<string> Batteries, HashSet<string> NetworkPorts,
        HashSet<string> Antiviruses)
    {
        public static ComputerSnapshot Capture(Computer c) => new(
            c.Name, c.SerialNumber, c.Manufacturer, c.Model,
            c.OperatingSystem, c.OsVersion, c.OsKernelVersion,
            c.HardwareUuid, c.ChassisType, c.TotalMemoryMb,
            c.LastLoggedUser, c.VmSystem, c.Domain, c.StatusId, c.LastInventoryAt,
            c.Components.Select(ComponentKey).ToHashSet(),
            c.Softwares.Select(SoftwareKey).ToHashSet(),
            c.Peripherals.Where(p => p.Kind == PeripheralKind.Monitor).Select(PeripheralKey).ToHashSet(),
            c.Volumes.Select(VolumeKey).ToHashSet(),
            c.Batteries.Select(BatteryKey).ToHashSet(),
            c.NetworkPorts.Select(NetworkPortKey).ToHashSet(),
            c.Antiviruses.Select(AntivirusKey).ToHashSet());
    }

    private static string ComponentKey(ComputerComponent c) =>
        $"{ComponentTypeLabel(c.Type)} {c.Designation}" + (c.Capacity is { Length: > 0 } cap ? $" ({cap})" : "");

    private static string SoftwareKey(ComputerSoftware s) => s.Version is { Length: > 0 } v ? $"{s.Name} ({v})" : s.Name;

    private static string PeripheralKey(ComputerPeripheral p) => p.Designation;

    private static string VolumeKey(ComputerVolume v) => v.MountPoint is { Length: > 0 } mp ? $"{v.Name} ({mp})" : v.Name;

    private static string BatteryKey(ComputerBattery b) => b.Serial is { Length: > 0 } s ? $"{b.Name} ({s})" : b.Name;

    private static string NetworkPortKey(ComputerNetworkPort p) => p.MacAddress is { Length: > 0 } mac ? $"{p.Designation} ({mac})" : p.Designation;

    private static string AntivirusKey(ComputerAntivirus a) => a.Version is { Length: > 0 } v ? $"{a.Name} ({v})" : a.Name;

    private static string ComponentTypeLabel(ComponentType type) => type switch
    {
        ComponentType.Cpu => "Processeur",
        ComponentType.Ram => "Barrette mémoire",
        ComponentType.Disk => "Disque dur",
        ComponentType.NetworkCard => "Carte réseau",
        ComponentType.Gpu => "Carte graphique",
        ComponentType.Motherboard => "Carte mère",
        _ => "Composant"
    };

    /// <summary>
    /// Construit les entrées d'historique en comparant <paramref name="before"/> (état capturé
    /// avant l'import) à l'état courant de <paramref name="computer"/> (après Apply*). Sur
    /// création (<paramref name="isNew"/>), on ne journalise que les ajouts de sous-éléments —
    /// pas les champs scalaires "null → valeur", qui ne correspondent à aucun changement observable.
    /// </summary>
    private static IEnumerable<ComputerHistoryEntry> BuildHistoryEntries(
        ComputerSnapshot before, Computer computer, bool isNew, string? beforeStatusLabel, string? afterStatusLabel)
    {
        if (!isNew)
        {
            if (before.Name != computer.Name) yield return FieldChange("Nom", before.Name, computer.Name);
            if (before.SerialNumber != computer.SerialNumber) yield return FieldChange("Numéro de série", before.SerialNumber, computer.SerialNumber);
            if (before.Manufacturer != computer.Manufacturer) yield return FieldChange("Fabricant", before.Manufacturer, computer.Manufacturer);
            if (before.Model != computer.Model) yield return FieldChange("Modèle", before.Model, computer.Model);
            if (before.OperatingSystem != computer.OperatingSystem) yield return FieldChange("Système d'exploitation", before.OperatingSystem, computer.OperatingSystem);
            if (before.OsVersion != computer.OsVersion) yield return FieldChange("Version de l'OS", before.OsVersion, computer.OsVersion);
            if (before.OsKernelVersion != computer.OsKernelVersion) yield return FieldChange("Version du noyau", before.OsKernelVersion, computer.OsKernelVersion);
            if (before.HardwareUuid != computer.HardwareUuid) yield return FieldChange("UUID matériel", before.HardwareUuid, computer.HardwareUuid);
            if (before.ChassisType != computer.ChassisType) yield return FieldChange("Type de châssis", before.ChassisType, computer.ChassisType);
            if (before.TotalMemoryMb != computer.TotalMemoryMb) yield return FieldChange("Mémoire totale", FormatMemory(before.TotalMemoryMb), FormatMemory(computer.TotalMemoryMb));
            if (before.LastLoggedUser != computer.LastLoggedUser) yield return FieldChange("Dernier utilisateur connecté", before.LastLoggedUser, computer.LastLoggedUser);
            if (before.VmSystem != computer.VmSystem) yield return FieldChange("Virtualisation", before.VmSystem, computer.VmSystem);
            if (before.Domain != computer.Domain) yield return FieldChange("Domaine", before.Domain, computer.Domain);
            if (before.LastInventoryAt != computer.LastInventoryAt) yield return FieldChange("Date de dernier inventaire", FormatDate(before.LastInventoryAt), FormatDate(computer.LastInventoryAt));
            if (before.StatusId != computer.StatusId) yield return FieldChange("Statut", beforeStatusLabel, afterStatusLabel);
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Components, computer.Components.Select(ComponentKey).ToHashSet(),
                     "Composants", "Ajouter un composant", "Supprimer un composant"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Softwares, computer.Softwares.Select(SoftwareKey).ToHashSet(),
                     "Logiciels", "Ajout d'un lien avec un élément", "Suppression du lien avec l'élément"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Monitors,
                     computer.Peripherals.Where(p => p.Kind == PeripheralKind.Monitor).Select(PeripheralKey).ToHashSet(),
                     "Périphériques", "Ajout d'un lien avec un élément", "Suppression du lien avec l'élément"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Volumes, computer.Volumes.Select(VolumeKey).ToHashSet(),
                     "Volumes", "Ajouter un volume", "Supprimer un volume"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Batteries, computer.Batteries.Select(BatteryKey).ToHashSet(),
                     "Batteries", "Ajouter un composant", "Supprimer un composant"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.NetworkPorts, computer.NetworkPorts.Select(NetworkPortKey).ToHashSet(),
                     "Ports réseau", "Ajouter un port réseau", "Supprimer un port réseau"))
        {
            yield return entry;
        }

        foreach (ComputerHistoryEntry entry in DiffKeyedSet(before.Antiviruses, computer.Antiviruses.Select(AntivirusKey).ToHashSet(),
                     "Antivirus", "Ajouter un antivirus", "Supprimer un antivirus"))
        {
            yield return entry;
        }
    }

    private static IEnumerable<ComputerHistoryEntry> DiffKeyedSet(HashSet<string> before, HashSet<string> after, string field, string addVerb, string removeVerb)
    {
        foreach (string added in after.Except(before))
        {
            yield return new ComputerHistoryEntry { User = HistoryUser, Field = field, Description = $"{addVerb} : {added}" };
        }

        foreach (string removed in before.Except(after))
        {
            yield return new ComputerHistoryEntry { User = HistoryUser, Field = field, Description = $"{removeVerb} : {removed}" };
        }
    }

    private static ComputerHistoryEntry FieldChange(string field, string? oldValue, string? newValue) => new()
    {
        User = HistoryUser,
        Field = field,
        Description = $"Changement de {oldValue ?? "—"} à {newValue ?? "—"}"
    };

    private static string? FormatMemory(int? memoryMb) => memoryMb is { } mb ? $"{mb} Mo" : null;

    private static string? FormatDate(DateTime? date) => date?.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

}
