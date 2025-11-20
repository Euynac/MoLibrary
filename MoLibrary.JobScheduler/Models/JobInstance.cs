using MoLibrary.JobScheduler.Jobs;

namespace MoLibrary.JobScheduler.Models;

/// <summary>
/// Represents a single execution instance of a job.
/// Job instances track the lifecycle of an individual job execution from creation to completion.
/// </summary>
public class JobInstance
{
    /// <summary>
    /// Gets or sets the unique identifier for this job instance.
    /// Generated as a GUID when the instance is created.
    /// Used as the cancellation token key in IMoCancellationManager for distributed cancellation.
    /// </summary>
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job key referencing the parent JobDefinition.
    /// Links this instance to its job definition for configuration and metadata.
    /// </summary>
    public string JobKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current execution state of this job instance.
    /// State transitions follow the state machine defined in JobState enum.
    /// </summary>
    public JobState State { get; set; }

    /// <summary>
    /// Gets or sets the JSON-serialized parameters for triggered jobs.
    /// Only applicable for <see cref="MoTriggeredJob{TParam}"/>. Null for recurring jobs.
    /// Deserialized and passed to the job's ExecuteAsync method.
    /// </summary>
    public string? JobArgs { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this job instance was created.
    /// Set when the instance is first created in the metadata store.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the scheduled execution time for delayed jobs.
    /// Only set for jobs with a delay (state starts as Scheduled).
    /// When this time arrives, the state transitions from Scheduled to Enqueued.
    /// </summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when job execution started.
    /// Set when a worker transitions the job to Processing state.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when job execution completed.
    /// Set when the job reaches a terminal state (Succeeded, Terminated, Cancelled, Skipped).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// Contains exception details, timeout messages, or other failure information.
    /// Populated when state transitions to Failed, Terminated, or Cancelled.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the current retry attempt number.
    /// Starts at 0 for the initial execution. Increments with each retry.
    /// Used to determine if the job should retry or transition to Terminated.
    /// </summary>
    public int RetryAttempt { get; set; } = 0;

    /// <summary>
    /// Gets or sets the client ID of the worker that is currently processing this job instance.
    /// Null if the job is not currently running.
    /// Used to track which worker is handling the job.
    /// </summary>
    public string? RunningClientId { get; set; }
}
