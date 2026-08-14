using Microsoft.EntityFrameworkCore;
using Monica.DependencyInjection.Abstractions;
using Monica.JobScheduler.EfCore.Entities;
using Monica.JobScheduler.Models;
using Monica.Repository.Persistence.Services;

namespace Monica.JobScheduler.EfCore;

/// <summary>
/// Owns the relational correctness boundary for the job catalog and durable execution queue.
/// </summary>
public sealed class JobSchedulerDbContext(
    DbContextOptions<JobSchedulerDbContext> options,
    ICachedServiceProvider serviceProvider)
    : RepositoryDbContext<JobSchedulerDbContext>(options, serviceProvider)
{
    internal DbSet<JobCatalogScopeEntity> CatalogScopes => Set<JobCatalogScopeEntity>();
    internal DbSet<JobCatalogReleaseEntity> CatalogReleases => Set<JobCatalogReleaseEntity>();
    internal DbSet<JobCatalogActivationEntity> CatalogActivations => Set<JobCatalogActivationEntity>();
    internal DbSet<JobPolicyEntity> JobPolicies => Set<JobPolicyEntity>();
    internal DbSet<JobExecutionEntity> Executions => Set<JobExecutionEntity>();
    internal DbSet<JobExecutionHistoryEntity> ExecutionHistory => Set<JobExecutionHistoryEntity>();
    internal DbSet<JobExecutionGateEntity> ExecutionGates => Set<JobExecutionGateEntity>();
    internal DbSet<JobWorkerCapabilityEntity> WorkerCapabilities => Set<JobWorkerCapabilityEntity>();
    internal DbSet<JobRecurringCursorEntity> RecurringCursors => Set<JobRecurringCursorEntity>();

    protected override void OnModelCreatingExtend(ModelBuilder modelBuilder)
    {
        base.OnModelCreatingExtend(modelBuilder);
        ConfigureCatalog(modelBuilder);
        ConfigureExecution(modelBuilder);
        RemoveInheritedStringDefaults(modelBuilder);
    }

    private static void ConfigureCatalog(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobCatalogScopeEntity>(entity =>
        {
            entity.ToTable("JobCatalogScopes");
            entity.HasKey(item => item.SchedulerScopeKey);
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.DesiredReleaseId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.LastSeenDeploymentReleaseId)
                .HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.ActiveReleaseId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        });

        modelBuilder.Entity<JobCatalogReleaseEntity>(entity =>
        {
            entity.ToTable("JobCatalogReleases");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.ReleaseId });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.ReleaseId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.ManifestContentHash).HasMaxLength(JobSchedulerIdentity.HASH_LENGTH);
            entity.Property(item => item.PayloadJson).HasColumnType("text");
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        });

        modelBuilder.Entity<JobCatalogActivationEntity>(entity =>
        {
            entity.ToTable("JobCatalogActivations");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.ActivationEpoch });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.ReleaseId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.ReleaseId });
        });

        modelBuilder.Entity<JobPolicyEntity>(entity =>
        {
            entity.ToTable("JobPolicies");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.JobKey });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.JobKey).HasMaxLength(JobSchedulerIdentity.JOB_KEY_MAX_LENGTH);
            entity.Property(item => item.ConcurrencyStamp).HasMaxLength(32);
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        });
    }

    private static void ConfigureExecution(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobExecutionEntity>(entity =>
        {
            entity.ToTable("JobExecutions");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.InstanceId });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.InstanceId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.TemplateJson).HasColumnType("text");
            entity.Property(item => item.CatalogReleaseId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.OwnerKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.WorkerRevisionId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.JobRevisionId).HasMaxLength(JobSchedulerIdentity.HASH_LENGTH);
            entity.Property(item => item.JobKey).HasMaxLength(JobSchedulerIdentity.JOB_KEY_MAX_LENGTH);
            entity.Property(item => item.JobArgs).HasColumnType("text");
            entity.Property(item => item.Origin).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.SkipReason).HasConversion<string>().HasMaxLength(40);
            entity.Property(item => item.State).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.RunningWorkerInstanceId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.ExecutionLeaseToken).HasMaxLength(32);
            entity.Property(item => item.CapabilityLeaseToken).HasMaxLength(32);
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
            entity.HasIndex(item => new
            {
                item.SchedulerScopeKey,
                item.State,
                item.AvailableAtUtcTicks,
                item.OwnerKey
            });
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.State, item.ActivationEpoch });
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.JobKey, item.State });
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.JobKey, item.CreatedAtUtcTicks });
            entity.HasMany(item => item.History)
                .WithOne(item => item.Execution)
                .HasForeignKey(item => new { item.SchedulerScopeKey, item.InstanceId })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<JobExecutionHistoryEntity>(entity =>
        {
            entity.ToTable("JobExecutionHistory");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.InstanceId, item.Sequence });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.InstanceId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.Kind).HasConversion<string>().HasMaxLength(30);
            entity.Property(item => item.PreviousState).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.NewState).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.LogLevel).HasConversion<string>().HasMaxLength(20);
            entity.Property(item => item.Message).HasColumnType("text");
            entity.Property(item => item.WorkerInstanceId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
        });

        modelBuilder.Entity<JobExecutionGateEntity>(entity =>
        {
            entity.ToTable("JobExecutionGates");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.JobKey });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.JobKey).HasMaxLength(JobSchedulerIdentity.JOB_KEY_MAX_LENGTH);
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        });

        modelBuilder.Entity<JobWorkerCapabilityEntity>(entity =>
        {
            entity.ToTable("JobWorkerCapabilities");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.WorkerInstanceId });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.WorkerInstanceId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.OwnerKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.WorkerRevisionId).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.JobRevisionIdsJson).HasColumnType("text");
            entity.Property(item => item.LeaseToken).HasMaxLength(32);
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.LeaseExpiresAtUtcTicks });
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.OwnerKey, item.LeaseExpiresAtUtcTicks });
        });

        modelBuilder.Entity<JobRecurringCursorEntity>(entity =>
        {
            entity.ToTable("JobRecurringCursors");
            entity.HasKey(item => new { item.SchedulerScopeKey, item.ActivationEpoch, item.JobRevisionId });
            entity.Property(item => item.SchedulerScopeKey).HasMaxLength(JobSchedulerIdentity.STANDARD_MAX_LENGTH);
            entity.Property(item => item.JobRevisionId).HasMaxLength(JobSchedulerIdentity.HASH_LENGTH);
            entity.Property(item => item.TemplateJson).HasColumnType("text");
            entity.Property(item => item.ScheduleJson).HasColumnType("text");
            entity.Property(item => item.SuspensionReasons).HasConversion<int>();
            entity.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
            entity.HasIndex(item => new { item.SchedulerScopeKey, item.ActivationEpoch, item.NextOccurrenceUtcTicks });
        });
    }

    private static void RemoveInheritedStringDefaults(ModelBuilder modelBuilder)
    {
        // Scheduler identities and payloads are always written explicitly. Database-side empty-string
        // defaults would weaken required-key validation and erase the meaning of nullable fields.
        foreach (var property in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(entityType => entityType.GetProperties())
                     .Where(property => property.ClrType == typeof(string)))
        {
            property.SetDefaultValue(null);
        }
    }
}
