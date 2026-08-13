using System.Security.Cryptography;
using System.Text.Json;

namespace Monica.JobScheduler.Models;

/// <summary>
/// Represents the complete code-owned declaration of one discovered job.
/// </summary>
/// <remarks>
/// Worker hosts publish these declarations as a project-owned snapshot. The registry control plane treats the
/// snapshot as authoritative for declaration fields, while preserving operator-owned runtime state such as whether
/// an existing job is disabled and its history-retention policy.
/// </remarks>
public sealed record JobDefinitionDeclaration
{
    /// <summary>
    /// Gets the stable job key, normally the full name of the implementation type.
    /// </summary>
    public required string JobKey { get; init; }

    /// <summary>
    /// Gets the argument type key for a triggered job, or <see langword="null"/> for a recurring job.
    /// </summary>
    public string? JobArgsKey { get; init; }

    /// <summary>
    /// Gets the project that owns and executes the job.
    /// </summary>
    public required string FromProject { get; init; }

    /// <summary>
    /// Gets the code-declared display name.
    /// </summary>
    public required string JobName { get; init; }

    /// <summary>
    /// Gets the code-declared description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets whether this is a recurring or triggered job.
    /// </summary>
    public JobType JobType { get; init; }

    /// <summary>
    /// Gets the code-declared maximum concurrency.
    /// </summary>
    public int MaxConcurrency { get; init; }

    /// <summary>
    /// Gets the code-declared retry count.
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Gets the code-declared maximum execution timeout.
    /// </summary>
    public TimeSpan MaxExecutionTimeout { get; init; }

    /// <summary>
    /// Gets whether a newly registered definition is disabled by default.
    /// Existing operator-owned enablement state is preserved during later reconciliation.
    /// </summary>
    public bool IsDisabledByDefault { get; init; }

    /// <summary>
    /// Gets the recurring cron expression, or <see langword="null"/> for a triggered job.
    /// </summary>
    public string? CronExpression { get; init; }

    /// <summary>
    /// Gets the optional code-declared recurring start boundary.
    /// </summary>
    public DateTime? StartTime { get; init; }

    /// <summary>
    /// Gets the optional code-declared recurring end boundary.
    /// </summary>
    public DateTime? EndTime { get; init; }

    internal static JobDefinitionDeclaration FromDefinition(JobDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new JobDefinitionDeclaration
        {
            JobKey = definition.JobKey,
            JobArgsKey = definition.JobArgsKey,
            FromProject = definition.FromProject,
            JobName = definition.JobName,
            Description = definition.Description,
            JobType = definition.JobType,
            MaxConcurrency = definition.MaxConcurrency,
            RetryCount = definition.RetryCount,
            MaxExecutionTimeout = definition.MaxExecutionTimeout,
            IsDisabledByDefault = definition.IsDisabled,
            CronExpression = definition.CronExpression,
            StartTime = definition.StartTime,
            EndTime = definition.EndTime
        };
    }

    internal JobDefinition Materialize(string schedulerScopeKey, JobDefinition? existing)
    {
        return new JobDefinition
        {
            SchedulerScopeKey = schedulerScopeKey,
            JobKey = JobKey,
            JobArgsKey = JobArgsKey,
            FromProject = FromProject,
            JobName = JobName,
            Description = Description,
            JobType = JobType,
            MaxConcurrency = MaxConcurrency,
            RetryCount = RetryCount,
            MaxExecutionTimeout = MaxExecutionTimeout,
            IsDisabled = existing?.IsDisabled ?? IsDisabledByDefault,
            IsDeleted = false,
            DeletedAt = null,
            MaxRetainedHistoryRecords = existing?.MaxRetainedHistoryRecords ?? 100,
            MaxRetentionDays = existing?.MaxRetentionDays,
            CronExpression = CronExpression,
            StartTime = StartTime,
            EndTime = EndTime
        };
    }
}

internal static class JobDefinitionSnapshotFingerprint
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new(JsonSerializerDefaults.Web);

    internal static string Compute(
        string schedulerScopeKey,
        string ownerProject,
        IEnumerable<JobDefinitionDeclaration> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerScopeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerProject);
        ArgumentNullException.ThrowIfNull(definitions);

        var canonical = new
        {
            SchedulerScopeKey = schedulerScopeKey,
            OwnerProject = ownerProject,
            Definitions = definitions
                .OrderBy(static definition => definition.JobKey, StringComparer.Ordinal)
                .Select(static definition => new
                {
                    definition.JobKey,
                    definition.JobArgsKey,
                    definition.FromProject,
                    definition.JobName,
                    definition.Description,
                    JobType = (int)definition.JobType,
                    definition.MaxConcurrency,
                    definition.RetryCount,
                    MaxExecutionTimeoutTicks = definition.MaxExecutionTimeout.Ticks,
                    definition.IsDisabledByDefault,
                    definition.CronExpression,
                    StartTimeTicks = definition.StartTime?.Ticks,
                    EndTimeTicks = definition.EndTime?.Ticks
                })
                .ToArray()
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, JSON_OPTIONS);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
