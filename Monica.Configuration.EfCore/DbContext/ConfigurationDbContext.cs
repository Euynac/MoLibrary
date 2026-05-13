using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.Configuration.EfCore.Entities;
using Monica.Repository.Persistence.Services;

namespace Monica.Configuration.EfCore.DbContext;

/// <summary>
/// EF Core context for Monica.Configuration persistence.
/// </summary>
public sealed class ConfigurationDbContext(
    DbContextOptions<ConfigurationDbContext> options,
    ICachedServiceProvider serviceProvider)
    : RepositoryDbContext<ConfigurationDbContext>(options, serviceProvider)
{
    public DbSet<ConfigurationDefinitionEntity> ConfigurationDefinitions => Set<ConfigurationDefinitionEntity>();

    public DbSet<ConfigurationValueOverrideEntity> ConfigurationValueOverrides => Set<ConfigurationValueOverrideEntity>();

    public DbSet<ConfigurationValueHistoryEntity> ConfigurationValueHistories => Set<ConfigurationValueHistoryEntity>();

    public DbSet<ConfigurationSourceStateEntity> ConfigurationSourceStates => Set<ConfigurationSourceStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConfigurationDefinitionEntity>().HasKey(x => x.DefinitionKey);
        modelBuilder.Entity<ConfigurationValueOverrideEntity>().HasKey(x => x.OverrideId);
        modelBuilder.Entity<ConfigurationValueOverrideEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.SourceKey, x.LogicalPath })
            .IsUnique();
        modelBuilder.Entity<ConfigurationValueOverrideEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.SourceKey, x.PathDepth });
        modelBuilder.Entity<ConfigurationValueHistoryEntity>().HasKey(x => x.HistoryId);
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.SourceKey, x.PathDepth, x.ModifiedTime });
        modelBuilder.Entity<ConfigurationSourceStateEntity>().HasKey(x => x.SourceKey);
    }
}
