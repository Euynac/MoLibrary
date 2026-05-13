using Microsoft.EntityFrameworkCore;
using Monica.Configuration.EfCore.Entities;

namespace Monica.Configuration.EfCore.DbContext;

/// <summary>
/// EF Core context for Monica.Configuration persistence.
/// </summary>
public sealed class ConfigurationDbContext(DbContextOptions<ConfigurationDbContext> options)
    : Microsoft.EntityFrameworkCore.DbContext(options)
{
    public DbSet<ConfigurationDefinitionEntity> ConfigurationDefinitions => Set<ConfigurationDefinitionEntity>();

    public DbSet<ConfigurationValueOverrideEntity> ConfigurationValueOverrides => Set<ConfigurationValueOverrideEntity>();

    public DbSet<ConfigurationValueHistoryEntity> ConfigurationValueHistories => Set<ConfigurationValueHistoryEntity>();

    public DbSet<ConfigurationSourceStateEntity> ConfigurationSourceStates => Set<ConfigurationSourceStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigurationDefinitionEntity>().HasKey(x => x.DefinitionKey);
        modelBuilder.Entity<ConfigurationValueOverrideEntity>().HasKey(x => x.OverrideId);
        modelBuilder.Entity<ConfigurationValueOverrideEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.LogicalPath })
            .IsUnique();
        modelBuilder.Entity<ConfigurationValueOverrideEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.PathDepth });
        modelBuilder.Entity<ConfigurationValueHistoryEntity>().HasKey(x => x.HistoryId);
        modelBuilder.Entity<ConfigurationSourceStateEntity>().HasKey(x => x.SourceKey);
    }
}
