using AnthoDingo.Setup;
using GlpiNg.Modules.Cron.Models;
using GlpiNg.Modules.Deployment.Models;
using GlpiNg.Modules.Inventory.Models;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Notifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Npgsql;
using System.Data.Common;

namespace GlpiNg.Web.Data;

public class GlpiNgDbContext(DbContextOptions<GlpiNgDbContext> options) : DbContext(options)
{
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<ComputerComponent> ComputerComponents => Set<ComputerComponent>();
    public DbSet<ComputerSoftware> ComputerSoftwares => Set<ComputerSoftware>();
    public DbSet<ComputerPeripheral> ComputerPeripherals => Set<ComputerPeripheral>();
    public DbSet<ComputerVolume> ComputerVolumes => Set<ComputerVolume>();
    public DbSet<ComputerBattery> ComputerBatteries => Set<ComputerBattery>();
    public DbSet<ComputerNetworkPort> ComputerNetworkPorts => Set<ComputerNetworkPort>();
    public DbSet<ComputerAntivirus> ComputerAntiviruses => Set<ComputerAntivirus>();
    public DbSet<ComputerImportHistory> ComputerImportHistories => Set<ComputerImportHistory>();
    public DbSet<ComputerHistoryEntry> ComputerHistoryEntries => Set<ComputerHistoryEntry>();
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<SavedSearchOrder> SavedSearchOrders => Set<SavedSearchOrder>();
    public DbSet<TableColumnPreference> TableColumnPreferences => Set<TableColumnPreference>();

    public DbSet<Peripheral> Peripherals => Set<Peripheral>();
    public DbSet<PeripheralHistoryEntry> PeripheralHistoryEntries => Set<PeripheralHistoryEntry>();

    public DbSet<GlpiAgent> Agents => Set<GlpiAgent>();
    public DbSet<DeploymentJob> DeploymentJobs => Set<DeploymentJob>();
    public DbSet<DeploymentPackage> DeploymentPackages => Set<DeploymentPackage>();
    public DbSet<DeploymentPackageFile> DeploymentPackageFiles => Set<DeploymentPackageFile>();
    public DbSet<DeploymentPackageFilePart> DeploymentPackageFileParts => Set<DeploymentPackageFilePart>();
    public DbSet<DeploymentPackageTarget> DeploymentPackageTargets => Set<DeploymentPackageTarget>();

    public DbSet<DeployComputerGroup> DeployComputerGroups => Set<DeployComputerGroup>();
    public DbSet<DeployComputerGroupMember> DeployComputerGroupMembers => Set<DeployComputerGroupMember>();
    public DbSet<DeployComputerGroupCriterion> DeployComputerGroupCriteria => Set<DeployComputerGroupCriterion>();

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

        modelBuilder.Entity<DeploymentPackage>()
            .HasMany(p => p.Files)
            .WithOne()
            .HasForeignKey(f => f.DeploymentPackageId);

        modelBuilder.Entity<DeploymentPackageFile>()
            .HasMany(f => f.Parts)
            .WithOne()
            .HasForeignKey(part => part.DeploymentPackageFileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeploymentPackageFilePart>()
            .HasIndex(part => part.Sha512)
            .IsUnique();

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

        // Clé primaire textuelle (nom de section, ex. "ParcSettings") plutôt qu'un Id auto-incrémenté :
        // longueur bornée nécessaire pour qu'une clé primaire soit indexable sous MySQL (utf8mb4).
        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.SectionName);

        modelBuilder.Entity<AppSetting>()
            .Property(s => s.SectionName)
            .HasMaxLength(100);
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
