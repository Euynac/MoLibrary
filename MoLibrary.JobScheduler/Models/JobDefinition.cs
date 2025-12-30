using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Represents the metadata and configuration for a job definition.
/// Job definitions are registered at application startup and define how jobs should be scheduled and executed.
/// </summary>
public class JobDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for this job definition. which is the job type's full name (TypeFullName).
    /// </summary>
    public required string JobKey { get; set; }
    
    /// <summary>
    /// Gets or sets the unique identifier for the job arguments type. which is the args type's full name (TypeFullName).
    /// </summary>
    public string? JobArgsKey { get; set; }

    /// <summary>
    /// Gets or sets the client project name where this job definition is defined.
    /// This is used to identify the source of the job definition in multi-project setups, which is also as the event topic
    /// for job queue.
    /// </summary>
    public required string FromProject { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name for this job.
    /// Defaults to the job type's simple name.
    /// Used for display purposes in UI and logs.
    /// </summary>
    public required string JobName { get; set; } 

    /// <summary>
    /// Gets or sets an optional description explaining the purpose of this job.
    /// Provides additional context about what the job does and when it should run.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the type of job (Recurring or Triggered).
    /// Determines whether the job is scheduled automatically or triggered on-demand.
    /// </summary>
    public JobType JobType { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent executions allowed for this job.
    /// Default is 1. When the limit is reached, new job instances will be skipped.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of automatic retry attempts on failure.
    /// Default is 0 (no retries). Failed jobs will retry up to this count before being terminated.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the maximum execution timeout for this job.
    /// Default is 1 hour. Jobs exceeding this duration will be cancelled via IMoCancellationManager.
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets whether this job is disabled.
    /// Default is false. Disabled jobs will not be scheduled or executed.
    /// </summary>
    public bool IsDisabled { get; set; } = false;

    /// <summary>
    /// Gets or sets whether this job definition has been soft deleted.
    /// Default is false. Soft deleted jobs are no longer active but preserved for audit purposes.
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// Gets or sets the timestamp when this job definition was soft deleted.
    /// Null if the job has never been deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retained history records for this job.
    /// When exceeded, the oldest records will be deleted during cleanup.
    /// Default is 100. Set to 0 or negative value to disable limit.
    /// </summary>
    public int MaxRetainedHistoryRecords { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum retention period in days for job execution history.
    /// Records older than this will be deleted during cleanup.
    /// Null means no time-based retention limit (only count-based limit applies).
    /// </summary>
    public int? MaxRetentionDays { get; set; }

    // Recurring job specific properties

    /// <summary>
    /// Gets or sets the cron expression for recurring job scheduling.
    /// Only applicable for recurring jobs. Supports second-level precision.
    /// Example: "*/10 * * * * *" runs every 10 seconds.
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the earliest time this recurring job should start executing.
    /// Only applicable for recurring jobs. Null means no start restriction.
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Gets or sets the latest time this recurring job should stop executing.
    /// Only applicable for recurring jobs. Null means no end restriction.
    /// </summary>
    public DateTime? EndTime { get; set; }

    // Metadata properties

    /// <summary>
    /// Gets or sets the CLR type of the job class for instantiation.
    /// Used to create job instances via dependency injection.
    /// Must inherit from RecurringJob or TriggeredJob{TParam}.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public Type JobClrType { get; set; } = typeof(object);

    /// <summary>
    /// Gets or sets the CLR type of the parameter for triggered jobs.
    /// Only applicable for TriggeredJob{TArgs}. Null for recurring jobs.
    /// Used to deserialize JSON parameters when creating job instances.
    /// </summary>
    [NotMapped]
    [JsonIgnore]
    public Type? JobArgsClrType { get; set; }

    /// <summary>
    /// Returns a string representation of the job definition.
    /// </summary>
    /// <returns>A string containing key information about the job definition.</returns>
    public override string ToString()
    {
        var statusFlags = new List<string>();
        if (IsDisabled) statusFlags.Add("Disabled");
        if (IsDeleted) statusFlags.Add("Deleted");
        var flags = statusFlags.Count > 0 ? $" [{string.Join(", ", statusFlags)}]" : "";

        var typeInfo = JobType == JobType.Recurring && !string.IsNullOrEmpty(CronExpression)
            ? $"{JobType} ({CronExpression})"
            : JobType.ToString();

        return $"JobDefinition[{JobKey}] {JobName} ({typeInfo}){flags}";
    }
}
