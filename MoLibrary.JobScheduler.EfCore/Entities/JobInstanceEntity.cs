using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoLibrary.JobScheduler.Models;
using MoLibrary.Repository.EntityInterfaces;

namespace MoLibrary.JobScheduler.EfCore.Entities;

/// <summary>
/// EF Core entity for persisting job instances.
/// </summary>
public class JobInstanceEntity : MoEntity<long>, IHasEntitySelfConfig<JobInstanceEntity>
{
    /// <summary>
    /// Unique identifier for this job instance (GUID).
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Job key referencing the parent JobDefinition.
    /// </summary>
    public string JobKey { get; set; } = string.Empty;

    /// <summary>
    /// Current execution state of this job instance.
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// JSON-serialized parameters for triggered jobs.
    /// </summary>
    public string? JobArgs { get; set; }

    /// <summary>
    /// Timestamp when this job instance was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when job execution started.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Timestamp when job execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Scheduled execution time for delayed jobs.
    /// </summary>
    public DateTime? ScheduledExecutionTime { get; set; }

    /// <summary>
    /// State change history (line-prefix format).
    /// </summary>
    public string? StateHistory { get; set; }

    /// <summary>
    /// Current retry attempt number.
    /// </summary>
    public int RetryAttempt { get; set; } = 0;

    /// <summary>
    /// Client ID of the worker currently processing this job instance.
    /// </summary>
    public string? RunningClientId { get; set; }

    public void Configure(EntityTypeBuilder<JobInstanceEntity> builder)
    {
        builder.ToTable("JobInstances");

        builder.HasKey(e => e.Id);

        // Unique index on InstanceId for efficient lookups
        builder.HasIndex(e => e.InstanceId)
            .IsUnique()
            .HasDatabaseName("IX_JobInstances_InstanceId");

        // Index on JobKey for filtering
        builder.HasIndex(e => e.JobKey)
            .HasDatabaseName("IX_JobInstances_JobKey");

        // Index on CreatedAt for sorting and range queries
        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("IX_JobInstances_CreatedAt");

        // Composite index for common query patterns
        builder.HasIndex(e => new { e.JobKey, e.State, e.CreatedAt })
            .HasDatabaseName("IX_JobInstances_JobKey_State_CreatedAt");

        // Composite index optimized for fetching latest instance per job (descending order)
        // This index is critical for batch queries in GetLatestInstancesAsync
        builder.HasIndex(e => new { e.JobKey, e.CreatedAt })
            .IsDescending(false, true) // JobKey ASC, CreatedAt DESC
            .HasDatabaseName("IX_JobInstances_JobKey_CreatedAt_Desc");

        builder.Property(e => e.InstanceId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.JobKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.State)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(e => e.JobArgs)
            .HasColumnType("text");

        builder.Property(e => e.StateHistory)
            .HasColumnType("text");

        builder.Property(e => e.RunningClientId)
            .HasMaxLength(100);
    }
}
