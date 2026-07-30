using GlpiNg.Modules.Inventory.Models;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GlpiAgent>()
            .HasIndex(a => a.DeviceId)
            .IsUnique();

        modelBuilder.Entity<Computer>()
            .HasOne(c => c.Agent)
            .WithOne(a => a.Computer)
            .HasForeignKey<Computer>(c => c.AgentId);

        modelBuilder.Entity<Computer>()
            .HasIndex(c => c.SourceGlpiId)
            .IsUnique()
            .HasFilter("\"SourceGlpiId\" IS NOT NULL");

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
    }
}
