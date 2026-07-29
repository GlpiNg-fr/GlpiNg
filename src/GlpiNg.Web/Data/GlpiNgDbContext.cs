using AnthoDingo.Setup;
using GlpiNg.Web.Models;
using GlpiNg.Web.Models.Agent;
using Microsoft.EntityFrameworkCore;

namespace GlpiNg.Web.Data;

public class GlpiNgDbContext(DbContextOptions<GlpiNgDbContext> options) : DbContext(options)
{
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<ComputerComponent> ComputerComponents => Set<ComputerComponent>();

    public DbSet<GlpiAgent> Agents => Set<GlpiAgent>();
    public DbSet<DeploymentJob> DeploymentJobs => Set<DeploymentJob>();
    public DbSet<DeploymentPackage> DeploymentPackages => Set<DeploymentPackage>();
    public DbSet<DeploymentPackageFile> DeploymentPackageFiles => Set<DeploymentPackageFile>();

    public DbSet<GlpiUser> Users => Set<GlpiUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GlpiAgent>()
            .HasIndex(a => a.AgentUuid)
            .IsUnique();

        modelBuilder.Entity<Computer>()
            .HasOne(c => c.Agent)
            .WithOne(a => a.Computer)
            .HasForeignKey<Computer>(c => c.AgentId);

        modelBuilder.Entity<DeploymentJob>()
            .HasOne(j => j.Agent)
            .WithMany(a => a.DeploymentJobs)
            .HasForeignKey(j => j.AgentId);

        modelBuilder.Entity<DeploymentJob>()
            .HasOne(j => j.Package)
            .WithMany()
            .HasForeignKey(j => j.PackageId);

        modelBuilder.Entity<DeploymentPackage>()
            .HasMany(p => p.Files)
            .WithOne()
            .HasForeignKey(f => f.DeploymentPackageId);

        modelBuilder.Entity<GlpiUser>()
            .HasIndex(u => u.UserName)
            .IsUnique();
    }

    /// <summary>
    /// Construit les options EF Core pour le <see cref="DbProvider"/> choisi lors de
    /// l'installation (AnthoDingo.Setup). Utilisée à la fois par
    /// <see cref="Services.GlpiNgSetupInitializer"/> — avant que la configuration finale ne
    /// soit chargée — et par l'enregistrement DI une fois l'installation terminée.
    /// SQLite n'est volontairement pas géré ici : seuls SQL Server, MySQL et PostgreSQL
    /// sont autorisés pour GlpiNg (voir <c>SetupOptions.AllowedProviders</c> dans Program.cs).
    /// </summary>
    public static void ConfigureProvider(DbContextOptionsBuilder builder, DbProvider provider, string connectionString)
    {
        switch (provider)
        {
            case DbProvider.SqlServer:
                builder.UseSqlServer(connectionString);
                break;
            case DbProvider.MySql:
                builder.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
                break;
            case DbProvider.Postgres:
                builder.UseNpgsql(connectionString);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider,
                    "GlpiNg n'autorise que SQL Server, MySQL et PostgreSQL.");
        }
    }

    /// <summary>Construit un GlpiNgDbContext autonome (hors DI) pour le provider et la chaîne donnés.</summary>
    public static GlpiNgDbContext Create(DbProvider provider, string connectionString)
    {
        DbContextOptionsBuilder<GlpiNgDbContext> optionsBuilder = new DbContextOptionsBuilder<GlpiNgDbContext>();
        ConfigureProvider(optionsBuilder, provider, connectionString);
        return new GlpiNgDbContext(optionsBuilder.Options);
    }
}
