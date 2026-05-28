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

    public DbSet<ConfigurationEffectiveValueEntity> ConfigurationEffectiveValues => Set<ConfigurationEffectiveValueEntity>();

    public DbSet<ConfigurationValueHistoryEntity> ConfigurationValueHistories => Set<ConfigurationValueHistoryEntity>();

    public DbSet<ConfigurationMutationGroupEntity> ConfigurationMutationGroups => Set<ConfigurationMutationGroupEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ConfigurationDefinitionEntity>().HasKey(x => x.DefinitionKey);
        modelBuilder.Entity<ConfigurationDefinitionEntity>()
            .Property(x => x.LastSeenTime)
            .HasConversion(value => ToUtcTicks(value), value => FromUtcTicks(value));
        modelBuilder.Entity<ConfigurationEffectiveValueEntity>().HasKey(x => x.DefinitionKey);
        modelBuilder.Entity<ConfigurationEffectiveValueEntity>()
            .Property(x => x.LastModifiedTime)
            .HasConversion(value => ToUtcTicks(value), value => FromUtcTicks(value));
        modelBuilder.Entity<ConfigurationValueHistoryEntity>().HasKey(x => x.HistoryId);
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .Property(x => x.ModifiedTime)
            .HasConversion(value => ToUtcTicks(value), value => FromUtcTicks(value));
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.PathDepth, x.ModifiedTime });
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .HasIndex(x => x.MutationGroupId);
        modelBuilder.Entity<ConfigurationMutationGroupEntity>().HasKey(x => x.GroupId);
        modelBuilder.Entity<ConfigurationMutationGroupEntity>()
            .Property(x => x.CreatedTime)
            .HasConversion(value => ToUtcTicks(value), value => FromUtcTicks(value));
        modelBuilder.Entity<ConfigurationMutationGroupEntity>()
            .Property(x => x.RolledBackTime)
            .HasConversion(value => ToNullableUtcTicks(value), value => FromNullableUtcTicks(value));
        modelBuilder.Entity<ConfigurationMutationGroupEntity>()
            .HasIndex(x => x.CreatedTime);
    }

    private static long ToUtcTicks(DateTimeOffset value)
    {
        return value.UtcDateTime.Ticks;
    }

    private static DateTimeOffset FromUtcTicks(long ticks)
    {
        return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
    }

    private static long? ToNullableUtcTicks(DateTimeOffset? value)
    {
        return value?.UtcDateTime.Ticks;
    }

    private static DateTimeOffset? FromNullableUtcTicks(long? ticks)
    {
        return ticks is null ? null : FromUtcTicks(ticks.Value);
    }
}
