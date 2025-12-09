using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoLibrary.JobScheduler.Models;
using MoLibrary.Repository.EntityInterfaces;

namespace MoLibrary.JobScheduler.EfCore.Entities;

/// <summary>
/// EF Core entity for persisting job definitions.
/// </summary>
public class JobDefinitionEntity : MoEntity<long>, IHasSoftDelete, IHasEntitySelfConfig<JobDefinitionEntity>
{
    /// <summary>
    /// Unique identifier for this job definition (typically the job type's full name).
    /// </summary>
    public string JobKey { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the job arguments type (the args type's full name).
    /// </summary>
    public string? JobArgsKey { get; set; }

    /// <summary>
    /// The client project name where this job definition is defined.
    /// </summary>
    public string FromProject { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for this job.
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description explaining the purpose of this job.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of job (Recurring or Triggered).
    /// </summary>
    public JobType JobType { get; set; }

    /// <summary>
    /// Maximum number of concurrent executions allowed for this job.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Number of automatic retry attempts on failure.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Maximum execution timeout in seconds.
    /// </summary>
    public long MaxExecutionTimeoutSeconds { get; set; } = 3600;

    /// <summary>
    /// Whether this job is disabled.
    /// </summary>
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    /// Whether this job definition has been soft deleted.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Timestamp when this job definition was soft deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Cron expression for recurring job scheduling.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Earliest time this recurring job should start executing.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Latest time this recurring job should stop executing.
    /// </summary>
    public DateTime? EndTime { get; set; }

    public void Configure(EntityTypeBuilder<JobDefinitionEntity> builder)
    {
        builder.ToTable("JobDefinitions");

        builder.HasKey(e => e.Id);

        // Unique index on JobKey for efficient lookups
        builder.HasIndex(e => e.JobKey)
            .IsUnique()
            .HasDatabaseName("IX_JobDefinitions_JobKey");

        // Index on FromProject for filtering
        builder.HasIndex(e => e.FromProject)
            .HasDatabaseName("IX_JobDefinitions_FromProject");

        builder.Property(e => e.JobKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.JobArgsKey)
            .HasMaxLength(500);

        builder.Property(e => e.FromProject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.JobName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.JobType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.CronExpression)
            .HasMaxLength(100);
    }
}
