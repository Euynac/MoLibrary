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
    private const string TIMESTAMP_WITH_TIME_ZONE_COLUMN_TYPE = "timestamp with time zone";
    private const string TIMESTAMP_COLUMN_TYPE = "timestamp";

    public DbSet<ConfigurationDefinitionEntity> ConfigurationDefinitions => Set<ConfigurationDefinitionEntity>();

    public DbSet<ConfigurationDefinitionPublishHistoryEntity> ConfigurationDefinitionPublishHistories =>
        Set<ConfigurationDefinitionPublishHistoryEntity>();

    public DbSet<ConfigurationEffectiveValueEntity> ConfigurationEffectiveValues => Set<ConfigurationEffectiveValueEntity>();

    public DbSet<ConfigurationValueHistoryEntity> ConfigurationValueHistories => Set<ConfigurationValueHistoryEntity>();

    public DbSet<ConfigurationMutationGroupEntity> ConfigurationMutationGroups => Set<ConfigurationMutationGroupEntity>();

    internal DbSet<ConfigurationSchemaMarkerEntity> ConfigurationSchemaMarkers => Set<ConfigurationSchemaMarkerEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Configuration values can include secrets, so this store never enables sensitive data logging implicitly.
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var timeColumnType = UsesTimestampWithTimeZone(Database.ProviderName)
            ? TIMESTAMP_WITH_TIME_ZONE_COLUMN_TYPE
            : TIMESTAMP_COLUMN_TYPE;

        modelBuilder.Entity<ConfigurationSchemaMarkerEntity>()
            .ToTable("ConfigurationSchemaMarkers")
            .HasKey(x => x.MarkerKey);
        modelBuilder.Entity<ConfigurationSchemaMarkerEntity>()
            .Property(x => x.MarkerKey)
            .IsRequired()
            .HasMaxLength(100);
        modelBuilder.Entity<ConfigurationSchemaMarkerEntity>()
            .Property(x => x.SchemaVersion)
            .IsRequired();
        modelBuilder.Entity<ConfigurationDefinitionEntity>().HasKey(x => x.DefinitionKey);
        modelBuilder.Entity<ConfigurationDefinitionEntity>()
            .HasIndex(x => x.FromProject);
        modelBuilder.Entity<ConfigurationDefinitionEntity>()
            .HasIndex(x => x.Category);
        modelBuilder.Entity<ConfigurationDefinitionEntity>()
            .Property(x => x.FromProject)
            .IsRequired()
            .HasMaxLength(200);
        modelBuilder.Entity<ConfigurationDefinitionEntity>()
            .Property(x => x.Category)
            .HasMaxLength(200);
        modelBuilder.Entity<ConfigurationDefinitionEntity>()
            .Property(x => x.PublishRevision)
            .IsConcurrencyToken();
        modelBuilder.Entity<ConfigurationDefinitionPublishHistoryEntity>().HasKey(x => x.HistoryId);
        modelBuilder.Entity<ConfigurationDefinitionPublishHistoryEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.PublishedTime });
        modelBuilder.Entity<ConfigurationDefinitionPublishHistoryEntity>()
            .Property(x => x.FromProject)
            .IsRequired()
            .HasMaxLength(200);
        modelBuilder.Entity<ConfigurationDefinitionPublishHistoryEntity>()
            .Property(x => x.Category)
            .HasMaxLength(200);
        modelBuilder.Entity<ConfigurationDefinitionPublishHistoryEntity>()
            .Property(x => x.PublishedTime)
            .HasPrecision(6)
            .HasColumnType(timeColumnType);
        modelBuilder.Entity<ConfigurationEffectiveValueEntity>().HasKey(x => x.DefinitionKey);
        modelBuilder.Entity<ConfigurationEffectiveValueEntity>()
            .Property(x => x.LastModifiedTime)
            .HasPrecision(6)
            .HasColumnType(timeColumnType);
        modelBuilder.Entity<ConfigurationValueHistoryEntity>().HasKey(x => x.HistoryId);
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .Property(x => x.ModifiedTime)
            .HasPrecision(6)
            .HasColumnType(timeColumnType);
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .HasIndex(x => new { x.DefinitionKey, x.PathDepth, x.ModifiedTime });
        modelBuilder.Entity<ConfigurationValueHistoryEntity>()
            .HasIndex(x => x.MutationGroupId);
        modelBuilder.Entity<ConfigurationMutationGroupEntity>().HasKey(x => x.GroupId);
        modelBuilder.Entity<ConfigurationMutationGroupEntity>()
            .Property(x => x.CreatedTime)
            .HasPrecision(6)
            .HasColumnType(timeColumnType);
        modelBuilder.Entity<ConfigurationMutationGroupEntity>()
            .Property(x => x.RolledBackTime)
            .HasPrecision(6)
            .HasColumnType(timeColumnType);
        modelBuilder.Entity<ConfigurationMutationGroupEntity>()
            .HasIndex(x => x.CreatedTime);
    }

    private static bool UsesTimestampWithTimeZone(string? providerName)
    {
        return providerName is not null
               && (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase)
                   || providerName.Contains("GaussDB", StringComparison.OrdinalIgnoreCase));
    }
}
