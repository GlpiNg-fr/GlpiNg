using AnthoDingo.Setup;
using GlpiNg.Modules.Abstractions.Entities;
using GlpiNg.Modules.Cron.Models;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Notifications;
using GlpiNg.Web.Models.ExternalLinks;
using GlpiNg.Web.Models.Webhooks;
using GlpiNg.Web.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using Npgsql;
using System.Data.Common;
using System.Reflection;

namespace GlpiNg.Web.Data;

public class GlpiNgDbContext(DbContextOptions<GlpiNgDbContext> options) : DbContext(options)
{
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<ComputerComponent> ComputerComponents => Set<ComputerComponent>();
    public DbSet<ComputerSoftware> ComputerSoftwares => Set<ComputerSoftware>();
    public DbSet<ComputerPeripheral> ComputerPeripherals => Set<ComputerPeripheral>();
    public DbSet<ComputerVolume> ComputerVolumes => Set<ComputerVolume>();
    public DbSet<ComputerBattery> ComputerBatteries => Set<ComputerBattery>();
    public DbSet<ComputerConnector> ComputerConnectors => Set<ComputerConnector>();
    public DbSet<ComputerNetworkPort> ComputerNetworkPorts => Set<ComputerNetworkPort>();
    public DbSet<ComputerAntivirus> ComputerAntiviruses => Set<ComputerAntivirus>();
    public DbSet<ComputerImportHistory> ComputerImportHistories => Set<ComputerImportHistory>();
    public DbSet<ComputerHistoryEntry> ComputerHistoryEntries => Set<ComputerHistoryEntry>();
    public DbSet<DictionaryRule> DictionaryRules => Set<DictionaryRule>();
    public DbSet<DictionaryRuleCriterion> DictionaryRuleCriteria => Set<DictionaryRuleCriterion>();
    public DbSet<DropdownItem> DropdownItems => Set<DropdownItem>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<SavedSearchOrder> SavedSearchOrders => Set<SavedSearchOrder>();
    public DbSet<TableColumnPreference> TableColumnPreferences => Set<TableColumnPreference>();
    public DbSet<LockedField> LockedFields => Set<LockedField>();

    public DbSet<Peripheral> Peripherals => Set<Peripheral>();
    public DbSet<PeripheralHistoryEntry> PeripheralHistoryEntries => Set<PeripheralHistoryEntry>();

    public DbSet<NetworkEquipment> NetworkEquipments => Set<NetworkEquipment>();
    public DbSet<NetworkEquipmentHistoryEntry> NetworkEquipmentHistoryEntries => Set<NetworkEquipmentHistoryEntry>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<PrinterHistoryEntry> PrinterHistoryEntries => Set<PrinterHistoryEntry>();
    public DbSet<Phone> Phones => Set<Phone>();
    public DbSet<PhoneHistoryEntry> PhoneHistoryEntries => Set<PhoneHistoryEntry>();
    public DbSet<Rack> Racks => Set<Rack>();
    public DbSet<RackHistoryEntry> RackHistoryEntries => Set<RackHistoryEntry>();
    public DbSet<Enclosure> Enclosures => Set<Enclosure>();
    public DbSet<EnclosureHistoryEntry> EnclosureHistoryEntries => Set<EnclosureHistoryEntry>();
    public DbSet<Pdu> Pdus => Set<Pdu>();
    public DbSet<PduHistoryEntry> PduHistoryEntries => Set<PduHistoryEntry>();
    public DbSet<PassiveEquipment> PassiveEquipments => Set<PassiveEquipment>();
    public DbSet<PassiveEquipmentHistoryEntry> PassiveEquipmentHistoryEntries => Set<PassiveEquipmentHistoryEntry>();

    public DbSet<CartridgeItem> CartridgeItems => Set<CartridgeItem>();
    public DbSet<Cartridge> Cartridges => Set<Cartridge>();
    public DbSet<CartridgeItemHistoryEntry> CartridgeItemHistoryEntries => Set<CartridgeItemHistoryEntry>();

    public DbSet<ConsumableItem> ConsumableItems => Set<ConsumableItem>();
    public DbSet<Consumable> Consumables => Set<Consumable>();
    public DbSet<ConsumableItemHistoryEntry> ConsumableItemHistoryEntries => Set<ConsumableItemHistoryEntry>();

    public DbSet<SimCard> SimCards => Set<SimCard>();
    public DbSet<SimCardHistoryEntry> SimCardHistoryEntries => Set<SimCardHistoryEntry>();

    public DbSet<Cable> Cables => Set<Cable>();
    public DbSet<CableHistoryEntry> CableHistoryEntries => Set<CableHistoryEntry>();

    public DbSet<GlpiAgent> Agents => Set<GlpiAgent>();
    public DbSet<DeploymentJob> DeploymentJobs => Set<DeploymentJob>();
    public DbSet<CollectResult> CollectResults => Set<CollectResult>();
    public DbSet<DeploymentPackage> DeploymentPackages => Set<DeploymentPackage>();
    public DbSet<DeploymentPackageFile> DeploymentPackageFiles => Set<DeploymentPackageFile>();
    public DbSet<DeploymentPackageFilePart> DeploymentPackageFileParts => Set<DeploymentPackageFilePart>();
    public DbSet<DeploymentPackageTarget> DeploymentPackageTargets => Set<DeploymentPackageTarget>();

    public DbSet<DeployComputerGroup> DeployComputerGroups => Set<DeployComputerGroup>();
    public DbSet<DeployComputerGroupMember> DeployComputerGroupMembers => Set<DeployComputerGroupMember>();
    public DbSet<DeployComputerGroupCriterion> DeployComputerGroupCriteria => Set<DeployComputerGroupCriterion>();
    public DbSet<DeploymentTask> DeploymentTasks => Set<DeploymentTask>();
    public DbSet<DeploymentTaskPackage> DeploymentTaskPackages => Set<DeploymentTaskPackage>();
    public DbSet<DeploymentTaskTarget> DeploymentTaskTargets => Set<DeploymentTaskTarget>();

    public DbSet<IpRange> IpRanges => Set<IpRange>();
    public DbSet<SnmpCredential> SnmpCredentials => Set<SnmpCredential>();
    public DbSet<NetworkTask> NetworkTasks => Set<NetworkTask>();
    public DbSet<NetworkTaskIpRange> NetworkTaskIpRanges => Set<NetworkTaskIpRange>();
    public DbSet<NetworkTaskCredential> NetworkTaskCredentials => Set<NetworkTaskCredential>();
    public DbSet<NetworkTaskActor> NetworkTaskActors => Set<NetworkTaskActor>();
    public DbSet<NetworkTaskJob> NetworkTaskJobs => Set<NetworkTaskJob>();
    public DbSet<DiscoveredNetworkDevice> DiscoveredNetworkDevices => Set<DiscoveredNetworkDevice>();

    public DbSet<DeploymentRule> DeploymentRules => Set<DeploymentRule>();

    public DbSet<WakeOnLanTask> WakeOnLanTasks => Set<WakeOnLanTask>();
    public DbSet<WakeOnLanTaskTarget> WakeOnLanTaskTargets => Set<WakeOnLanTaskTarget>();
    public DbSet<WakeOnLanTaskActor> WakeOnLanTaskActors => Set<WakeOnLanTaskActor>();
    public DbSet<WakeOnLanTaskJob> WakeOnLanTaskJobs => Set<WakeOnLanTaskJob>();

    public DbSet<GlpiUser> Users => Set<GlpiUser>();
    public DbSet<GlpiUserHistoryEntry> UserHistoryEntries => Set<GlpiUserHistoryEntry>();
    public DbSet<GlpiUserProfile> UserProfiles => Set<GlpiUserProfile>();
    public DbSet<DashboardCardPreference> DashboardCardPreferences => Set<DashboardCardPreference>();

    public DbSet<GlpiGroup> Groups => Set<GlpiGroup>();
    public DbSet<GlpiGroupUser> GroupUsers => Set<GlpiGroupUser>();
    public DbSet<GlpiGroupNote> GroupNotes => Set<GlpiGroupNote>();
    public DbSet<GlpiGroupHistoryEntry> GroupHistoryEntries => Set<GlpiGroupHistoryEntry>();

    public DbSet<GlpiEntity> Entities => Set<GlpiEntity>();
    public DbSet<GlpiEntityNote> EntityNotes => Set<GlpiEntityNote>();
    public DbSet<GlpiEntityHistoryEntry> EntityHistoryEntries => Set<GlpiEntityHistoryEntry>();

    public DbSet<GlpiProfile> Profiles => Set<GlpiProfile>();
    public DbSet<GlpiProfileHistoryEntry> ProfileHistoryEntries => Set<GlpiProfileHistoryEntry>();

    public DbSet<AuthLdapServer> AuthLdapServers => Set<AuthLdapServer>();
    public DbSet<AuthMailServer> AuthMailServers => Set<AuthMailServer>();

    public DbSet<OAuthClient> OAuthClients => Set<OAuthClient>();

    public DbSet<CronSettings> CronSettings => Set<CronSettings>();

    public DbSet<AutomaticActionState> AutomaticActionStates => Set<AutomaticActionState>();
    public DbSet<AutomaticActionRunLog> AutomaticActionRunLogs => Set<AutomaticActionRunLog>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<TimeSlotEntry> TimeSlotEntries => Set<TimeSlotEntry>();

    public DbSet<CollectDefinition> CollectDefinitions => Set<CollectDefinition>();
    public DbSet<CollectRegistryEntry> CollectRegistryEntries => Set<CollectRegistryEntry>();
    public DbSet<CollectWmiEntry> CollectWmiEntries => Set<CollectWmiEntry>();
    public DbSet<CollectFileSearchEntry> CollectFileSearchEntries => Set<CollectFileSearchEntry>();

    public DbSet<DeploymentMirrorServer> DeploymentMirrorServers => Set<DeploymentMirrorServer>();
    public DbSet<DeploymentUserInteractionTemplate> DeploymentUserInteractionTemplates => Set<DeploymentUserInteractionTemplate>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRecipient> NotificationRecipients => Set<NotificationRecipient>();
    public DbSet<QueuedNotification> QueuedNotifications => Set<QueuedNotification>();

    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookHeader> WebhookHeaders => Set<WebhookHeader>();
    public DbSet<QueuedWebhook> QueuedWebhooks => Set<QueuedWebhook>();

    public DbSet<ExternalLink> ExternalLinks => Set<ExternalLink>();
    public DbSet<ExternalLinkItemType> ExternalLinkItemTypes => Set<ExternalLinkItemType>();

    public DbSet<EventLogEntry> EventLogEntries => Set<EventLogEntry>();

    public DbSet<ComputerRule> ComputerRules => Set<ComputerRule>();

    public DbSet<ImportAssignmentRule> ImportAssignmentRules => Set<ImportAssignmentRule>();
    public DbSet<ImportBlacklistEntry> ImportBlacklistEntries => Set<ImportBlacklistEntry>();
    public DbSet<RefusedImportLog> RefusedImportLogs => Set<RefusedImportLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GlpiUser>()
            .HasIndex(u => u.UserName)
            .IsUnique();

        modelBuilder.Entity<DashboardCardPreference>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        modelBuilder.Entity<GlpiAgent>()
            .HasIndex(a => a.AgentUuid)
            .IsUnique();

        modelBuilder.Entity<Computer>()
            .HasOne(c => c.Agent)
            .WithOne(a => a.Computer)
            .HasForeignKey<Computer>(c => c.AgentId);

        modelBuilder.Entity<Computer>()
            .HasIndex(c => c.SourceGlpiId)
            .IsUnique()
            .HasFilter("\"SourceGlpiId\" IS NOT NULL");

        // SetNull plutôt que le Cascade par défaut d'EF pour une FK optionnelle : détacher ou
        // supprimer l'ordinateur connecté ne doit pas supprimer le périphérique lui-même, qui
        // reste un actif géré indépendamment (voir Peripheral.ComputerId).
        modelBuilder.Entity<Peripheral>()
            .HasOne(p => p.Computer)
            .WithMany()
            .HasForeignKey(p => p.ComputerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Pas de navigation GlpiAgent.DeploymentJobs : DeploymentJob (module Deploy,
        // dans GlpiNg.Web) référence l'agent (module Inventory) par sa seule clé
        // étrangère, pour ne pas faire dépendre le module Inventory du module Deploy.
        modelBuilder.Entity<DeploymentJob>()
            .HasOne(j => j.Agent)
            .WithMany()
            .HasForeignKey(j => j.AgentId);

        modelBuilder.Entity<DeploymentJob>()
            .HasOne(j => j.Package)
            .WithMany()
            .HasForeignKey(j => j.PackageId);

        // Restrict (et non SetNull comme les autres références optionnelles de DeploymentTask
        // juste en dessous) : SQL Server refuse deux chemins de cascade SET NULL depuis la même
        // table vers la même table cible (ici DeploymentTasks -> TimeSlots via ces deux colonnes),
        // "may cause cycles or multiple cascade paths" — il faut donc bloquer la suppression d'un
        // créneau encore référencé plutôt que de la laisser mettre PreparationTimeSlotId/
        // ExecutionTimeSlotId à null. L'admin doit d'abord retirer le créneau des tâches qui le
        // référencent (champs purement déclaratifs pour l'instant, voir la doc de DeploymentTask).
        modelBuilder.Entity<DeploymentTask>()
            .HasOne(t => t.PreparationTimeSlot)
            .WithMany()
            .HasForeignKey(t => t.PreparationTimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DeploymentTask>()
            .HasOne(t => t.ExecutionTimeSlot)
            .WithMany()
            .HasForeignKey(t => t.ExecutionTimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade sur les deux extrémités : une ligne DeploymentTaskPackage n'a de sens que
        // rattachée à sa tâche ET à son paquet — supprimer l'un ou l'autre retire juste ce paquet
        // de la liste de la tâche (qui peut retomber à zéro paquet, état normal juste après
        // création, voir la doc de DeploymentTask) plutôt que de bloquer la suppression.
        modelBuilder.Entity<DeploymentTask>()
            .HasMany(t => t.Packages)
            .WithOne()
            .HasForeignKey(p => p.DeploymentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentTaskPackage>()
            .HasOne(p => p.Package)
            .WithMany()
            .HasForeignKey(p => p.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentTaskPackage>()
            .HasIndex(p => new { p.DeploymentTaskId, p.PackageId })
            .IsUnique();

        // Même principe que DeploymentTaskPackage ci-dessus, pour les acteurs (groupe ou
        // ordinateur individuel, voir DeploymentTaskTarget) : supprimer le groupe/ordinateur ciblé
        // retire juste cet acteur de la tâche plutôt que de bloquer sa suppression.
        modelBuilder.Entity<DeploymentTask>()
            .HasMany(t => t.Targets)
            .WithOne()
            .HasForeignKey(tg => tg.DeploymentTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentTaskTarget>()
            .HasOne(tg => tg.Group)
            .WithMany()
            .HasForeignKey(tg => tg.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentTaskTarget>()
            .HasOne(tg => tg.Computer)
            .WithMany()
            .HasForeignKey(tg => tg.ComputerId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull : supprimer une tâche ne doit pas emporter l'historique des jobs qu'elle a créés
        // (même principe que QueuedNotification.NotificationId plus bas) — le job reste, simplement
        // sans tâche d'origine.
        modelBuilder.Entity<DeploymentJob>()
            .HasOne(j => j.Task)
            .WithMany(t => t.Jobs)
            .HasForeignKey(j => j.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Pas de navigation GlpiAgent.NetworkTaskJobs / .NetworkTaskActors : même raison que
        // DeploymentJob.Agent plus haut (ne pas faire dépendre le module Inventory du module Deploy).
        modelBuilder.Entity<NetworkTaskJob>()
            .HasOne(j => j.Agent)
            .WithMany()
            .HasForeignKey(j => j.AgentId);

        // Cascade (et non SetNull comme DeploymentJob.Task) : contrairement à un DeploymentJob, qui
        // ne fait que référencer son paquet, la spec d'un NetworkTaskJob (plages IP + identifiants)
        // est lue en direct depuis sa NetworkTask au moment de "getNetDiscoveryJobs"/
        // "getNetInventoryJobs" (voir la doc de NetworkTaskJob) — un job sans tâche n'a plus de sens
        // et perdre la tâche perd donc aussi l'historique des jobs qu'elle a créés.
        modelBuilder.Entity<NetworkTaskJob>()
            .HasOne(j => j.Task)
            .WithMany(t => t.Jobs)
            .HasForeignKey(j => j.NetworkTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict : même raison multi-chemin-cascade SQL Server que DeploymentTask.ExecutionTimeSlot
        // plus haut ("may cause cycles or multiple cascade paths").
        modelBuilder.Entity<NetworkTask>()
            .HasOne(t => t.ExecutionTimeSlot)
            .WithMany()
            .HasForeignKey(t => t.ExecutionTimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Cascade sur les deux extrémités, même principe que DeploymentTaskPackage/DeploymentTaskTarget
        // plus haut : une ligne de jointure n'a de sens que rattachée à sa tâche ET à sa plage/son
        // identifiant/son agent — supprimer l'un ou l'autre retire juste cette entrée de la liste de
        // la tâche plutôt que de bloquer la suppression.
        modelBuilder.Entity<NetworkTask>()
            .HasMany(t => t.IpRanges)
            .WithOne()
            .HasForeignKey(r => r.NetworkTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkTaskIpRange>()
            .HasOne(r => r.IpRange)
            .WithMany()
            .HasForeignKey(r => r.IpRangeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkTaskIpRange>()
            .HasIndex(r => new { r.NetworkTaskId, r.IpRangeId })
            .IsUnique();

        modelBuilder.Entity<NetworkTask>()
            .HasMany(t => t.Credentials)
            .WithOne()
            .HasForeignKey(c => c.NetworkTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkTaskCredential>()
            .HasOne(c => c.SnmpCredential)
            .WithMany()
            .HasForeignKey(c => c.SnmpCredentialId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkTaskCredential>()
            .HasIndex(c => new { c.NetworkTaskId, c.SnmpCredentialId })
            .IsUnique();

        modelBuilder.Entity<NetworkTask>()
            .HasMany(t => t.Actors)
            .WithOne()
            .HasForeignKey(a => a.NetworkTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkTaskActor>()
            .HasOne(a => a.Agent)
            .WithMany()
            .HasForeignKey(a => a.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NetworkTaskActor>()
            .HasIndex(a => new { a.NetworkTaskId, a.AgentId })
            .IsUnique();

        // SetNull : l'historique de découverte (adresse/MAC/sysDescr...) reste utile même une fois
        // la tâche qui l'a produit supprimée ou l'équipement promu vers un NetworkEquipment.
        modelBuilder.Entity<DiscoveredNetworkDevice>()
            .HasOne(d => d.DiscoveredViaNetworkTask)
            .WithMany()
            .HasForeignKey(d => d.DiscoveredViaNetworkTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<DiscoveredNetworkDevice>()
            .HasOne(d => d.PromotedNetworkEquipment)
            .WithMany()
            .HasForeignKey(d => d.PromotedNetworkEquipmentId)
            .OnDelete(DeleteBehavior.SetNull);

        // WakeOnLanTask : même famille de configuration que DeploymentTask (cibles Group/Computer)
        // et NetworkTask (agents relais, ExecutionTimeSlot Restrict) combinées — voir les
        // commentaires détaillés sur ces deux blocs plus haut, non répétés ici.
        modelBuilder.Entity<WakeOnLanTask>()
            .HasOne(t => t.ExecutionTimeSlot)
            .WithMany()
            .HasForeignKey(t => t.ExecutionTimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WakeOnLanTask>()
            .HasMany(t => t.Targets)
            .WithOne()
            .HasForeignKey(tg => tg.WakeOnLanTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WakeOnLanTaskTarget>()
            .HasOne(tg => tg.Group)
            .WithMany()
            .HasForeignKey(tg => tg.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WakeOnLanTaskTarget>()
            .HasOne(tg => tg.Computer)
            .WithMany()
            .HasForeignKey(tg => tg.ComputerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WakeOnLanTask>()
            .HasMany(t => t.RelayAgents)
            .WithOne()
            .HasForeignKey(a => a.WakeOnLanTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WakeOnLanTaskActor>()
            .HasOne(a => a.Agent)
            .WithMany()
            .HasForeignKey(a => a.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WakeOnLanTaskActor>()
            .HasIndex(a => new { a.WakeOnLanTaskId, a.AgentId })
            .IsUnique();

        // Pas de navigation GlpiAgent.WakeOnLanTaskJobs : même raison que DeploymentJob.Agent/
        // NetworkTaskJob.Agent plus haut.
        modelBuilder.Entity<WakeOnLanTaskJob>()
            .HasOne(j => j.Agent)
            .WithMany()
            .HasForeignKey(j => j.AgentId);

        // Cascade : les cibles sont figées dans TargetMacsJson au lancement (voir la doc de
        // WakeOnLanTaskJob), mais un job n'a de sens que rattaché à la tâche qui l'a créé — même
        // raisonnement que NetworkTaskJob.Task.
        modelBuilder.Entity<WakeOnLanTaskJob>()
            .HasOne(j => j.Task)
            .WithMany(t => t.Jobs)
            .HasForeignKey(j => j.WakeOnLanTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade des deux côtés : un résultat de collecte n'a de sens ni sans le poste qu'il
        // décrit, ni sans la collecte qui l'a demandé. L'index le rend consultable par poste —
        // c'est la seule lecture que fait la fiche — et garantit une valeur par entrée : un
        // nouveau passage remplace le précédent au lieu de s'empiler.
        modelBuilder.Entity<CollectResult>()
            .HasOne(result => result.CollectDefinition)
            .WithMany()
            .HasForeignKey(result => result.CollectDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollectResult>()
            .HasIndex(result => new { result.ComputerId, result.CollectDefinitionId, result.EntryName })
            .IsUnique();

        modelBuilder.Entity<DeploymentPackage>()
            .HasMany(p => p.Files)
            .WithOne()
            .HasForeignKey(f => f.DeploymentPackageId);

        modelBuilder.Entity<DeploymentPackageFile>()
            .HasMany(f => f.Parts)
            .WithOne()
            .HasForeignKey(part => part.DeploymentPackageFileId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index de recherche, et non contrainte d'unicité : un fragment est adressé par le hash de
        // son contenu (voir AgentController.GetDeployFilePart), mais il appartient à un fichier,
        // lui-même à un paquet. Deux paquets qui embarquent le même fichier — cas courant d'un
        // import depuis GLPI — produisent donc deux fois les mêmes fragments, et un fichier dont
        // deux morceaux sont identiques en produit deux aussi. L'unicité était intenable, et sa
        // violation faisait échouer l'enregistrement de tout l'import.
        //
        // Les doublons sont sans conséquence à la lecture : le hash désigne un contenu, donc
        // n'importe laquelle des lignes qui le portent mène aux mêmes octets.
        modelBuilder.Entity<DeploymentPackageFilePart>()
            .HasIndex(part => part.Sha512);

        // SetNull : supprimer le groupe désactive juste le déploiement à la demande du paquet
        // (retombe sur "-----", comme plugin_glpiinventory_deploygroups_id côté GLPI-Inventory
        // d'origine) plutôt que de supprimer le paquet lui-même.
        modelBuilder.Entity<DeploymentPackage>()
            .HasOne(p => p.DeployComputerGroup)
            .WithMany()
            .HasForeignKey(p => p.DeployComputerGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // Cascade : une cible n'a de sens que rattachée à son paquet. Pas de contrainte FK vers
        // Entities/Groups/Profiles/Users : DeploymentPackageTarget.ItemId est une référence
        // polymorphe (Type + ItemId, comme itemtype/items_id côté GLPI) que EF Core ne peut pas
        // contraindre vers plusieurs tables cibles — voir la doc de DeploymentPackageTarget.
        modelBuilder.Entity<DeploymentPackage>()
            .HasMany(p => p.Targets)
            .WithOne()
            .HasForeignKey(t => t.DeploymentPackageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict comme GlpiGroup.Parent plus bas : FK auto-référencée, donc pas de Cascade
        // (non supporté par SQL Server sur une relation qui boucle sur la même table). Empêche
        // aussi de supprimer un paquet remplaçant tant que d'autres paquets le référencent encore.
        modelBuilder.Entity<DeploymentPackage>()
            .HasOne(p => p.SupersededByPackage)
            .WithMany()
            .HasForeignKey(p => p.SupersededByPackageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GlpiGroup>()
            .HasOne(g => g.Parent)
            .WithMany(g => g.Children)
            .HasForeignKey(g => g.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GlpiGroupUser>()
            .HasKey(gu => new { gu.GroupId, gu.UserId });

        modelBuilder.Entity<GlpiGroupUser>()
            .HasOne(gu => gu.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(gu => gu.GroupId);

        modelBuilder.Entity<GlpiGroupUser>()
            .HasOne(gu => gu.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(gu => gu.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiEntity>()
            .HasOne(e => e.Parent)
            .WithMany(e => e.Children)
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotence de l'import GLPI "Administration" (voir Import/GlpiAdminMySqlImportService),
        // même principe que Computer.SourceGlpiId plus haut.
        modelBuilder.Entity<GlpiEntity>()
            .HasIndex(e => e.SourceGlpiId)
            .IsUnique()
            .HasFilter("\"SourceGlpiId\" IS NOT NULL");

        modelBuilder.Entity<GlpiGroup>()
            .HasIndex(g => g.SourceGlpiId)
            .IsUnique()
            .HasFilter("\"SourceGlpiId\" IS NOT NULL");

        modelBuilder.Entity<GlpiProfile>()
            .HasIndex(p => p.SourceGlpiId)
            .IsUnique()
            .HasFilter("\"SourceGlpiId\" IS NOT NULL");

        modelBuilder.Entity<GlpiUser>()
            .HasIndex(u => u.SourceGlpiId)
            .IsUnique()
            .HasFilter("\"SourceGlpiId\" IS NOT NULL");

        modelBuilder.Entity<GlpiUserProfile>()
            .HasOne(up => up.User)
            .WithMany(u => u.Habilitations)
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiUserProfile>()
            .HasOne(up => up.Entity)
            .WithMany()
            .HasForeignKey(up => up.EntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GlpiUserProfile>()
            .HasOne(up => up.Profile)
            .WithMany()
            .HasForeignKey(up => up.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<GlpiEntityNote>()
            .HasOne(n => n.Entity)
            .WithMany()
            .HasForeignKey(n => n.EntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiEntityHistoryEntry>()
            .HasOne<GlpiEntity>()
            .WithMany()
            .HasForeignKey(h => h.EntityId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiGroupNote>()
            .HasOne(n => n.Group)
            .WithMany()
            .HasForeignKey(n => n.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiGroupHistoryEntry>()
            .HasOne<GlpiGroup>()
            .WithMany()
            .HasForeignKey(h => h.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiUserHistoryEntry>()
            .HasOne<GlpiUser>()
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<GlpiProfileHistoryEntry>()
            .HasOne<GlpiProfile>()
            .WithMany()
            .HasForeignKey(h => h.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AuthLdapServer>()
            .HasIndex(s => s.Name)
            .IsUnique();

        modelBuilder.Entity<AuthMailServer>()
            .HasIndex(s => s.Name)
            .IsUnique();

        // SetNull plutôt que Restrict/Cascade : supprimer un annuaire LDAP ne doit pas empêcher
        // sa suppression ni supprimer les comptes qui s'y authentifiaient — ils retombent
        // simplement sans LdapServerId (AuthSource reste Ldap, la connexion échouera jusqu'à
        // rattachement à un autre annuaire ou retour en local via un nouveau mot de passe).
        modelBuilder.Entity<GlpiUser>()
            .HasOne(u => u.LdapServer)
            .WithMany()
            .HasForeignKey(u => u.LdapServerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OAuthClient>()
            .HasIndex(c => c.ClientId)
            .IsUnique();

        modelBuilder.Entity<OAuthClient>()
            .HasIndex(c => c.Name)
            .IsUnique();

        modelBuilder.Entity<SavedSearchOrder>()
            .HasIndex(o => new { o.UserId, o.SavedSearchId })
            .IsUnique();

        modelBuilder.Entity<TableColumnPreference>()
            .HasIndex(p => new { p.UserId, p.ItemType })
            .IsUnique();

        modelBuilder.Entity<AutomaticActionState>()
            .HasIndex(s => s.TaskKey)
            .IsUnique();

        modelBuilder.Entity<AutomaticActionRunLog>()
            .HasIndex(l => new { l.TaskKey, l.RanAt });

        modelBuilder.Entity<TimeSlot>()
            .HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TimeSlotId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollectDefinition>()
            .HasMany(c => c.RegistryEntries)
            .WithOne()
            .HasForeignKey(e => e.CollectDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollectDefinition>()
            .HasMany(c => c.WmiEntries)
            .WithOne()
            .HasForeignKey(e => e.CollectDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollectDefinition>()
            .HasMany(c => c.FileSearchEntries)
            .WithOne()
            .HasForeignKey(e => e.CollectDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeployComputerGroup>()
            .HasMany(g => g.Members)
            .WithOne()
            .HasForeignKey(m => m.DeployComputerGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeployComputerGroupMember>()
            .HasOne(m => m.Computer)
            .WithMany()
            .HasForeignKey(m => m.ComputerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeployComputerGroupMember>()
            .HasIndex(m => new { m.DeployComputerGroupId, m.ComputerId })
            .IsUnique();

        modelBuilder.Entity<DeployComputerGroup>()
            .HasMany(g => g.Criteria)
            .WithOne()
            .HasForeignKey(c => c.DeployComputerGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentRule>()
            .HasMany(r => r.Criteria)
            .WithOne()
            .HasForeignKey(c => c.DeploymentRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentRule>()
            .HasMany(r => r.Actions)
            .WithOne()
            .HasForeignKey(a => a.DeploymentRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Même principe que DeploymentTaskPackage->DeploymentPackage (voir plus haut) : supprimer
        // le paquet retire juste cette action de la règle plutôt que de bloquer sa suppression.
        modelBuilder.Entity<DeploymentRuleAction>()
            .HasOne(a => a.Package)
            .WithMany()
            .HasForeignKey(a => a.PackageId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DictionaryRule>()
            .HasMany(r => r.Criteria)
            .WithOne()
            .HasForeignKey(c => c.DictionaryRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComputerRule>()
            .HasMany(r => r.Criteria)
            .WithOne()
            .HasForeignKey(c => c.ComputerRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ComputerRule>()
            .HasMany(r => r.Actions)
            .WithOne()
            .HasForeignKey(a => a.ComputerRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ImportAssignmentRule>()
            .HasMany(r => r.Criteria)
            .WithOne()
            .HasForeignKey(c => c.ImportAssignmentRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ImportAssignmentRule>()
            .HasMany(r => r.Actions)
            .WithOne()
            .HasForeignKey(a => a.ImportAssignmentRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unicité d'une valeur d'Intitulé, dans son type, son entité et sous son parent.
        //
        // Le parent en fait partie parce que les Lieux sont hiérarchisés (Site > Bâtiment > Salle) :
        // « Bureau » doit pouvoir exister sous deux bâtiments différents, comme dans GLPI, dont la
        // clé d'unicité est (entities_id, locations_id, name). Un index sur (Type, Name) seul
        // l'interdisait.
        //
        // L'entité en fait partie pour la même raison depuis le cloisonnement : deux entités
        // tiennent chacune leur propre référentiel d'intitulés.
        //
        // Les types plats (Fabricant, Statut...) n'ont jamais de parent : pour eux, la contrainte
        // reste équivalente à (Type, Name) au sein d'une entité, ce dont dépend l'upsert « création
        // à la volée » de Computers/Detail.razor.cs (SaveComputerFieldsAsync).
        modelBuilder.Entity<DropdownItem>()
            .HasIndex(i => new { i.Type, i.EntityId, i.ParentId, i.Name })
            .IsUnique();

        // Restrict comme GlpiGroup.Parent plus bas : FK auto-référencée, donc pas de Cascade (non
        // supporté par SQL Server sur une relation qui boucle sur la même table). Empêche aussi de
        // supprimer un Lieu tant que des sous-lieux le référencent encore.
        modelBuilder.Entity<DropdownItem>()
            .HasOne(i => i.Parent)
            .WithMany()
            .HasForeignKey(i => i.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull sur les trois entités qui référencent un Intitulé de type Status (Computer,
        // Peripheral, ComputerPeripheral/"Moniteurs") : supprimer une valeur de statut depuis
        // /config/dropdowns ne doit pas empêcher sa suppression ni supprimer les actifs qui la
        // référencent, ils retombent simplement sans statut.
        modelBuilder.Entity<Computer>()
            .HasOne(c => c.StatusItem)
            .WithMany()
            .HasForeignKey(c => c.StatusId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Peripheral>()
            .HasOne(p => p.StatusItem)
            .WithMany()
            .HasForeignKey(p => p.StatusId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ComputerPeripheral>()
            .HasOne(p => p.StatusItem)
            .WithMany()
            .HasForeignKey(p => p.StatusId)
            .OnDelete(DeleteBehavior.SetNull);

        // Restrict (et non SetNull comme StatusId ci-dessus) : SQL Server refuse un deuxième chemin
        // de cascade SET NULL depuis Computers vers DropdownItems (même limitation que
        // DeploymentTask.PreparationTimeSlot/ExecutionTimeSlot plus haut, "may cause cycles or
        // multiple cascade paths"). L'admin doit d'abord retirer ce Lieu des postes qui le
        // référencent avant de le supprimer depuis /config/dropdowns.
        modelBuilder.Entity<Computer>()
            .HasOne(c => c.LocationItem)
            .WithMany()
            .HasForeignKey(c => c.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Nouveaux types de parc (Matériel réseau, Imprimante, Téléphone, Baie, Châssis, PDU,
        // Équipement passif) : même paire StatusId(SetNull)/LocationId(Restrict) que Computer
        // ci-dessus, plutôt que les champs texte libres Site/Building/Room de Peripheral (qui
        // datent d'avant l'introduction des Intitulés).
        modelBuilder.Entity<NetworkEquipment>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<NetworkEquipment>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Printer>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Printer>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Phone>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Phone>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Rack>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Rack>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Enclosure>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Enclosure>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Pdu>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Pdu>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PassiveEquipment>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<PassiveEquipment>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<SimCard>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SimCard>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);

        // Cartouches (CartridgeItem = modèle, Cartridge = exemplaire individuel suivi en stock,
        // voir leur doc dans Models/CartridgeItem.cs) : Cascade pour Cartridges/HistoryEntries
        // (n'ont de sens que rattachés à leur modèle), SetNull pour le lien optionnel vers
        // l'imprimante dans laquelle une cartouche est en service (retirer/supprimer
        // l'imprimante ne doit pas supprimer la cartouche, qui retombe simplement en stock).
        modelBuilder.Entity<CartridgeItem>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        // WithOne(c => c.CartridgeItem) : la navigation inverse sert à nommer une unité vue depuis
        // l'imprimante, où seul le numéro d'exemplaire serait autrement disponible. Même relation,
        // même clé étrangère — rien ne change au schéma.
        modelBuilder.Entity<CartridgeItem>()
            .HasMany(ci => ci.Cartridges)
            .WithOne(c => c.CartridgeItem!)
            .HasForeignKey(c => c.CartridgeItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CartridgeItem>()
            .HasMany(ci => ci.HistoryEntries)
            .WithOne()
            .HasForeignKey(h => h.CartridgeItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Cartridge>()
            .HasOne(c => c.Printer)
            .WithMany()
            .HasForeignKey(c => c.PrinterId)
            .OnDelete(DeleteBehavior.SetNull);

        // Consommables (ConsumableItem/Consumable) : même principe que Cartouches ci-dessus, sans
        // lien vers une imprimante.
        modelBuilder.Entity<ConsumableItem>().HasOne(e => e.LocationItem).WithMany().HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<ConsumableItem>()
            .HasMany(ci => ci.Consumables)
            .WithOne()
            .HasForeignKey(c => c.ConsumableItemId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ConsumableItem>()
            .HasMany(ci => ci.HistoryEntries)
            .WithOne()
            .HasForeignKey(h => h.ConsumableItemId)
            .OnDelete(DeleteBehavior.Cascade);

        // Câbles (Cable) : EndpointAType/EndpointAId et EndpointBType/EndpointBId sont une
        // référence polymorphe non contrainte (voir doc dans Models/Cable.cs), donc aucune config
        // de clé étrangère ici pour les extrémités — seulement StatusItem et HistoryEntries.
        modelBuilder.Entity<Cable>().HasOne(e => e.StatusItem).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Cable>()
            .HasMany(c => c.HistoryEntries)
            .WithOne()
            .HasForeignKey(h => h.CableId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentMirrorServer>()
            .HasIndex(s => s.Name)
            .IsUnique();

        modelBuilder.Entity<DeploymentUserInteractionTemplate>()
            .HasIndex(t => t.Name)
            .IsUnique();

        // Restrict : un gabarit encore référencé par une notification ne doit pas pouvoir être
        // supprimé silencieusement (la notification se retrouverait sans contenu à envoyer) —
        // l'admin doit d'abord réassigner ou supprimer la notification elle-même.
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Template)
            .WithMany()
            .HasForeignKey(n => n.NotificationTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasMany(n => n.Recipients)
            .WithOne(r => r.Notification)
            .HasForeignKey(r => r.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull : supprimer l'utilisateur/le groupe ciblé par un destinataire ne doit pas
        // supprimer la notification elle-même (comme GlpiUser.LdapServerId plus haut) — la ligne
        // de destinataire reste, simplement sans cible résolue tant qu'elle n'est pas corrigée.
        modelBuilder.Entity<NotificationRecipient>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<NotificationRecipient>()
            .HasOne(r => r.Group)
            .WithMany()
            .HasForeignKey(r => r.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // SetNull : une notification supprimée ne doit pas emporter l'historique déjà envoyé/en
        // attente dans la file — voir QueuedNotification.NotificationName, qui garde le nom même
        // une fois la référence perdue.
        modelBuilder.Entity<QueuedNotification>()
            .HasOne(q => q.Notification)
            .WithMany()
            .HasForeignKey(q => q.NotificationId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Webhook>()
            .HasMany(w => w.Headers)
            .WithOne(h => h.Webhook)
            .HasForeignKey(h => h.WebhookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Même choix que QueuedNotification juste au-dessus : supprimer un webhook ne doit pas
        // effacer la trace de ce qu'il a déjà envoyé — voir QueuedWebhook.WebhookName, qui garde
        // le nom une fois la référence perdue.
        modelBuilder.Entity<QueuedWebhook>()
            .HasOne(q => q.Webhook)
            .WithMany()
            .HasForeignKey(q => q.WebhookId)
            .OnDelete(DeleteBehavior.SetNull);

        // La file est lue par statut puis par ancienneté (tâche cron) et par ancienneté seule
        // (page de consultation) : c'est le seul index qui compte ici.
        modelBuilder.Entity<QueuedWebhook>()
            .HasIndex(q => new { q.Status, q.CreatedAt });

        modelBuilder.Entity<ExternalLink>()
            .HasMany(link => link.ItemTypes)
            .WithOne(association => association.ExternalLink)
            .HasForeignKey(association => association.ExternalLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un même type ne s'associe qu'une fois à un lien : deux lignes identiques feraient
        // apparaître le lien en double sur chaque fiche concernée.
        modelBuilder.Entity<ExternalLinkItemType>()
            .HasIndex(association => new { association.ExternalLinkId, association.ItemType })
            .IsUnique();

        // C'est la requête de chaque ouverture de fiche : « quels liens pour ce type ? »
        modelBuilder.Entity<ExternalLinkItemType>()
            .HasIndex(association => association.ItemType);

        // Clé primaire textuelle (nom de section, ex. "ParcSettings") plutôt qu'un Id auto-incrémenté :
        // longueur bornée nécessaire pour qu'une clé primaire soit indexable sous MySQL (utf8mb4).
        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.SectionName);

        modelBuilder.Entity<AppSetting>()
            .Property(s => s.SectionName)
            .HasMaxLength(100);

        // Un verrou ne peut exister qu'une fois par champ et par élément — la pose est idempotente
        // côté écran, cet index en fait une garantie.
        modelBuilder.Entity<LockedField>()
            .HasIndex(l => new { l.ItemType, l.ItemId, l.Field })
            .IsUnique();

        ConfigureEntityScoping(modelBuilder);
    }

    /// <summary>
    /// Cloisonnement par entité : pour chaque type implémentant <see cref="IEntityScoped"/>,
    /// pose la FK optionnelle vers <see cref="GlpiEntity"/>, un index sur la colonne, et le
    /// filtre global de visibilité.
    ///
    /// Fait par balayage du modèle plutôt qu'entité par entité : les 35 types concernés sont
    /// répartis entre l'hôte et les modules Inventory/Deployment, et un type ajouté plus tard est
    /// pris en compte du seul fait qu'il implémente l'interface — impossible d'oublier un filtre.
    ///
    /// <see cref="DeleteBehavior.Restrict"/> : supprimer une entité ne doit surtout pas emporter
    /// en cascade les objets qui y sont rattachés.
    /// </summary>
    private void ConfigureEntityScoping(ModelBuilder modelBuilder)
    {
        MethodInfo apply = typeof(GlpiNgDbContext)
            .GetMethod(nameof(ApplyEntityScoping), BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IEntityScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            apply.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyEntityScoping<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IEntityScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasOne<GlpiEntity>()
            .WithMany()
            .HasForeignKey(e => e.EntityId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TEntity>()
            .HasIndex(e => e.EntityId);

        // Règle de visibilité de GLPI, voir EntityScope. Le filtre lit EntityScope sur l'instance
        // de contexte : il est donc re-paramétré à chaque requête (EF traite l'accès au membre
        // comme une variable capturée), et c'est la fabrique de contextes qui l'a positionné —
        // voir EntityScopedDbContextFactory. Un contexte non estampillé reste sur
        // EntityScope.Unrestricted, ce qui neutralise le filtre : c'est le cas du protocole agent,
        // des tâches cron et des imports.
        modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
            EntityScope.IsUnrestricted
            || e.EntityId == null
            || EntityScope.VisibleEntityIds.Contains(e.EntityId.Value)
            || (e.IsRecursive && EntityScope.AncestorEntityIds.Contains(e.EntityId.Value)));
    }

    /// <summary>
    /// Cloisonnement applicable aux requêtes de ce contexte. <see cref="EntityScope.Unrestricted"/>
    /// par défaut : un contexte obtenu autrement que par la fabrique cloisonnée (protocole agent,
    /// cron, imports, services singleton via <c>IRootDbContextFactory</c>) voit tout.
    /// </summary>
    public EntityScope EntityScope { get; set; } = EntityScope.Unrestricted;

    /// <summary>
    /// Rattache tout nouvel objet cloisonné à l'entité active, quand le formulaire ne l'a pas fait
    /// lui-même. Évite d'avoir à ajouter le champ « Entité » aux quelque 40 formulaires de
    /// l'application pour que la création soit correcte : un objet créé depuis l'entité active y
    /// est rattaché, ce qui est le comportement de GLPI.
    /// </summary>
    /// <summary>
    /// Droits applicables aux écritures de ce contexte. <see cref="ProfileRights.Full"/> par
    /// défaut, comme <see cref="EntityScope"/> : un contexte obtenu autrement que par la fabrique
    /// (protocole agent, cron, imports, <see cref="IRootDbContextFactory"/>) écrit sans contrôle.
    /// </summary>
    public ProfileRights ProfileRights { get; set; } = ProfileRights.Full;

    /// <summary>
    /// Refuse une écriture sur un type dont l'utilisateur n'a pas le droit en écriture. Filet de
    /// sécurité de second rang : la protection de premier rang est le garde-fou de routes, qui
    /// empêche d'atteindre l'écran. Il attrape ce que le garde-fou de routes ne peut pas voir —
    /// une écriture déclenchée depuis une page d'une autre section, un composant partagé — et
    /// couvre d'un coup les quelque 40 formulaires, dont aucun bouton n'a besoin d'être modifié.
    ///
    /// Lève plutôt que d'ignorer silencieusement : une écriture refusée doit se voir.
    /// </summary>
    private void EnforceWriteRights()
    {
        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            ProfileSection? section = ProfileSectionMap.ForEntityType(entry.Entity.GetType());

            if (section is not null && !ProfileRights.CanWrite(section.Value))
            {
                throw new UnauthorizedAccessException(
                    $"Droits insuffisants pour modifier « {entry.Entity.GetType().Name} » : le profil n'a pas le droit d'écriture sur la section « {section} ».");
            }
        }
    }

    private void StampActiveEntityOnNewEntries()
    {
        if (EntityScope.ActiveEntityId is not int activeEntityId)
        {
            return;
        }

        foreach (EntityEntry entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IEntityScoped scoped && scoped.EntityId is null)
            {
                scoped.EntityId = activeEntityId;
            }
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceWriteRights();
        StampActiveEntityOnNewEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnforceWriteRights();
        StampActiveEntityOnNewEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Construit les options EF Core pour le <see cref="DbProvider"/> choisi lors de
    /// l'installation (AnthoDingo.Setup). Utilisée à la fois par
    /// <see cref="Services.GlpiNgSetupInitializer"/> — avant que la configuration finale ne
    /// soit chargée — et par l'enregistrement DI une fois l'installation terminée.
    /// SQLite n'est volontairement pas géré ici : seuls SQL Server, MySQL et PostgreSQL
    /// sont autorisés pour GlpiNg (voir <c>SetupOptions.AllowedProviders</c> dans Program.cs).
    /// </summary>
    /// <param name="builder">Le builder d'options EF Core à configurer.</param>
    /// <param name="provider">Le provider de base de données choisi lors de l'installation.</param>
    /// <param name="connectionString">La chaîne de connexion vers le serveur principal.</param>
    /// <param name="fallbackConnectionString">
    /// Chaîne de connexion vers un serveur de secours, optionnelle. Si renseignée, le serveur
    /// principal (<paramref name="connectionString"/>) est sondé au démarrage ; s'il n'est pas
    /// joignable, c'est le serveur de secours qui est utilisé à sa place.
    /// </param>
    public static void ConfigureProvider(DbContextOptionsBuilder builder, DbProvider provider, string connectionString,
        string? fallbackConnectionString = null)
    {
        string effectiveConnectionString = connectionString;
        if (!string.IsNullOrWhiteSpace(fallbackConnectionString) && !CanConnect(provider, connectionString))
        {
            effectiveConnectionString = fallbackConnectionString;
        }

        switch (provider)
        {
            case DbProvider.SqlServer:
                builder.UseSqlServer(effectiveConnectionString);
                break;
            case DbProvider.MySql:
                builder.UseMySql(effectiveConnectionString, ServerVersion.AutoDetect(effectiveConnectionString));
                break;
            case DbProvider.Postgres:
                builder.UseNpgsql(effectiveConnectionString);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider,
                    "GlpiNg n'autorise que SQL Server, MySQL et PostgreSQL.");
        }
    }

    /// <summary>Construit un GlpiNgDbContext autonome (hors DI) pour le provider et la chaîne donnés.</summary>
    public static GlpiNgDbContext Create(DbProvider provider, string connectionString, string? fallbackConnectionString = null)
    {
        DbContextOptionsBuilder<GlpiNgDbContext> optionsBuilder = new DbContextOptionsBuilder<GlpiNgDbContext>();
        ConfigureProvider(optionsBuilder, provider, connectionString, fallbackConnectionString);
        return new GlpiNgDbContext(optionsBuilder.Options);
    }

    /// <summary>
    /// Sonde la joignabilité d'un serveur en tentant une connexion ADO.NET avec un timeout
    /// court (indépendant du "Connect Timeout" propre à la chaîne, pensé lui pour l'usage
    /// normal de l'application) : on ne veut pas bloquer le démarrage le temps du timeout
    /// complet avant de basculer sur le serveur de secours.
    /// </summary>
    private static bool CanConnect(DbProvider provider, string connectionString)
    {
        const int probeTimeoutSeconds = 3;
        try
        {
            using DbConnection connection = provider switch
            {
                DbProvider.SqlServer => new SqlConnection(new SqlConnectionStringBuilder(connectionString)
                {
                    ConnectTimeout = probeTimeoutSeconds
                }.ConnectionString),
                DbProvider.MySql => new MySqlConnection(new MySqlConnectionStringBuilder(connectionString)
                {
                    ConnectionTimeout = probeTimeoutSeconds
                }.ConnectionString),
                DbProvider.Postgres => new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString)
                {
                    Timeout = probeTimeoutSeconds
                }.ConnectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(provider), provider,
                    "GlpiNg n'autorise que SQL Server, MySQL et PostgreSQL.")
            };
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
