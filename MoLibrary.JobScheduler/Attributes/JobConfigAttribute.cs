namespace MoLibrary.JobScheduler.Attributes;

/// <summary>
/// Consolidated attribute for configuring job metadata and behavior.
/// Apply this attribute to classes inheriting from RecurringJob or TriggeredJob{TParam}
/// to override default configuration values.
/// All properties are optional - if not specified, defaults from ModuleJobSchedulerOption or
/// system defaults will be used.
/// </summary>
/// <example>
/// <code>
/// [JobConfig(
///     JobName = "Daily Report Generator",
///     Description = "Generates daily sales reports",
///     CronSchedule = "0 0 8 * * *",  // Every day at 8 AM
///     MaxConcurrency = 1,
///     RetryCount = 3,
///     MaxExecutionTimeoutSeconds = 300,
///     StartTime = "2025-01-01T00:00:00",
///     EndTime = "2025-12-31T23:59:59"
/// )]
/// public class DailyReportJob : RecurringTask
/// {
///     public override async Task ExecuteAsync(CancellationToken cancellationToken)
///     {
///         // Implementation
///     }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class JobConfigAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the unique identifier for the job.
    /// If not specified, defaults to the job type's full name (TypeFullName).
    /// Must be unique across all registered jobs in the system.
    /// </summary>
    public string? JobKey { get; set; }

    /// <summary>
    /// Gets or sets the human-readable name for the job.
    /// If not specified, defaults to the job type's simple name.
    /// Used for display in UI and logs.
    /// </summary>
    public string? JobName { get; set; }

    /// <summary>
    /// Gets or sets an optional description explaining the job's purpose.
    /// Provides context about what the job does and when it should run.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent executions allowed for this job.
    /// Use this property in attribute declarations.
    /// If not specified, defaults to 1.
    /// When the limit is reached, new job instances will be marked as Skipped.
    /// Set to higher values for jobs that can safely run concurrently.
    /// Example: [JobConfig(MaxConcurrency = 5)]
    /// </summary>
    public int MaxConcurrency
    {
        get => _maxConcurrency;
        set
        {
            _maxConcurrency = value;
            MaxConcurrencyBridge = value;
        }
    }

    /// <summary>
    /// Gets or sets the number of automatic retry attempts on failure.
    /// Use this property in attribute declarations.
    /// If not specified, defaults to 0 (no retries).
    /// Failed jobs will retry up to this count before being terminated.
    /// Example: [JobConfig(RetryCount = 3)]
    /// </summary>
    public int RetryCount
    {
        get => _retryCount;
        set
        {
            _retryCount = value;
            RetryCountBridge = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum execution timeout in seconds.
    /// Use this property in attribute declarations.
    /// If not specified, defaults to 3600 seconds (1 hour).
    /// Jobs exceeding this duration will be cancelled via IMoCancellationManager.
    /// Example: [JobConfig(MaxExecutionTimeoutSeconds = 300)] // 5 minutes
    /// </summary>
    public int MaxExecutionTimeoutSeconds
    {
        get => _maxExecutionTimeoutSeconds;
        set
        {
            _maxExecutionTimeoutSeconds = value;
            MaxExecutionTimeout = TimeSpan.FromSeconds(value);
        }
    }

    /// <summary>
    /// Gets or sets the cron expression for recurring job scheduling.
    /// Only applicable for RecurringJob. Ignored for TriggeredJob.
    /// Supports second-level precision using 6-field cron syntax: "second minute hour day month dayOfWeek"
    /// Examples: "*/10 * * * * *" (every 10 seconds), "0 */5 * * * *" (every 5 minutes),
    ///           "0 0 8 * * *" (every day at 8 AM), "0 30 9 * * 1-5" (weekdays at 9:30 AM)
    /// </summary>
    public string? CronSchedule { get; set; }

    /// <summary>
    /// Gets or sets the earliest time this recurring job should start executing.
    /// Use this property in attribute declarations.
    /// Only applicable for RecurringJob. Ignored for TriggeredJob.
    /// If not specified, the job can execute immediately based on cron schedule.
    /// Format: ISO 8601 date-time string (e.g., "2025-01-01T00:00:00")
    /// Example: [JobConfig(StartTime = "2025-01-01T00:00:00")]
    /// </summary>
    public string? StartTime
    {
        get => _startTime;
        set
        {
            _startTime = value;
            StartTimeBridge = string.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value);
        }
    }

    /// <summary>
    /// Gets or sets the latest time this recurring job should stop executing.
    /// Use this property in attribute declarations.
    /// Only applicable for RecurringJob. Ignored for TriggeredJob.
    /// If not specified, the job will continue executing indefinitely based on cron schedule.
    /// Format: ISO 8601 date-time string (e.g., "2025-12-31T23:59:59")
    /// Example: [JobConfig(EndTime = "2025-12-31T23:59:59")]
    /// </summary>
    public string? EndTime
    {
        get => _endTime;
        set
        {
            _endTime = value;
            EndTimeBridge = string.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value);
        }
    }

    /// <summary>
    /// Gets or sets whether this job is disabled.
    /// Use this property in attribute declarations.
    /// If not specified, defaults to false (job is enabled).
    /// Disabled jobs will not be scheduled or executed until re-enabled.
    /// Example: [JobConfig(IsDisabled = true)]
    /// </summary>
    public bool IsDisabled
    {
        get => _isDisabled;
        set
        {
            _isDisabled = value;
            IsDisabledBridge = value;
        }
    }

    #region Bridge Properties
    private int _maxConcurrency;
    internal int? MaxConcurrencyBridge { get; private set; }

    private int _retryCount;
    internal int? RetryCountBridge { get; private set; }

    private int _maxExecutionTimeoutSeconds;
    internal TimeSpan? MaxExecutionTimeout { get; private set; }

    private string? _startTime;
    internal DateTime? StartTimeBridge { get; private set; }

    private string? _endTime;
    internal DateTime? EndTimeBridge { get; private set; }

    private bool _isDisabled;
    internal bool? IsDisabledBridge { get; private set; }
    #endregion
}
