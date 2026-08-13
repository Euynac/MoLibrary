using Microsoft.Extensions.Logging;
using Monica.JobScheduler.Models;
using Monica.JobScheduler.Models.Execution;

namespace Monica.JobScheduler.EfCore.Entities;

internal sealed class JobCatalogScopeEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string? DesiredReleaseId { get; set; }
    public string? LastSeenDeploymentReleaseId { get; set; }
    public long LastSeenDeploymentGeneration { get; set; }
    public long DesiredIntentEpoch { get; set; }
    public string? ActiveReleaseId { get; set; }
    public long ActiveIntentEpoch { get; set; }
    public long PublicationEpoch { get; set; }
    public long ActivationEpoch { get; set; }
    public long ChangeEpoch { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class JobCatalogReleaseEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string ReleaseId { get; set; } = string.Empty;
    public string ManifestContentHash { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class JobCatalogActivationEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public long ActivationEpoch { get; set; }
    public string ReleaseId { get; set; } = string.Empty;
    public long ActivatedAtUtcTicks { get; set; }
    public long DesiredIntentEpoch { get; set; }
    public bool IsExplicitReactivation { get; set; }
    public int RetiredQueuedExecutionCount { get; set; }
    public int RunningCancellationRequestCount { get; set; }
}

// Operator policy follows the scope-wide logical JobKey across owner transfers and release reactivation. Owner is an
// update fence resolved from the active catalog, not part of the durable policy identity.
internal sealed class JobPolicyEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public bool? DisabledOverride { get; set; }
    public int? MaxRetainedHistoryRecords { get; set; }
    public int? MaxRetentionDays { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public long UpdatedAtUtcTicks { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class JobExecutionEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string TemplateJson { get; set; } = string.Empty;
    public string CatalogReleaseId { get; set; } = string.Empty;
    public long ActivationEpoch { get; set; }
    public string OwnerKey { get; set; } = string.Empty;
    public string WorkerRevisionId { get; set; } = string.Empty;
    public string JobRevisionId { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public string? JobArgs { get; set; }
    public long AvailableAtUtcTicks { get; set; }
    public JobExecutionState State { get; set; }
    public long CreatedAtUtcTicks { get; set; }
    public long? StartedAtUtcTicks { get; set; }
    public long? CompletedAtUtcTicks { get; set; }
    public int ExecutionAttempt { get; set; }
    public int RetryAttempt { get; set; }
    public int LeaseLossCount { get; set; }
    public string? RunningWorkerInstanceId { get; set; }
    public string? ExecutionLeaseToken { get; set; }
    public string? CapabilityLeaseToken { get; set; }
    public long? LeaseExpiresAtUtcTicks { get; set; }
    public long? CancellationRequestedAtUtcTicks { get; set; }
    public int NextHistorySequence { get; set; } = 1;
    public Guid ConcurrencyToken { get; set; }
    public List<JobExecutionHistoryEntity> History { get; set; } = [];
}

internal sealed class JobExecutionHistoryEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public long TimestampUtcTicks { get; set; }
    public JobExecutionHistoryKind Kind { get; set; }
    public JobExecutionState? PreviousState { get; set; }
    public JobExecutionState? NewState { get; set; }
    public LogLevel LogLevel { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? WorkerInstanceId { get; set; }
    public JobExecutionEntity Execution { get; set; } = null!;
}

// A gate belongs to the scope-wide logical job. Owner and revision remain execution-routing identities only; including
// them here would let a replacement owner overlap work that is still cooperatively stopping after catalog cutover.
internal sealed class JobExecutionGateEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string JobKey { get; set; } = string.Empty;
    public int ActiveCount { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class JobWorkerCapabilityEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public string WorkerInstanceId { get; set; } = string.Empty;
    public string OwnerKey { get; set; } = string.Empty;
    public string WorkerRevisionId { get; set; } = string.Empty;
    public string JobRevisionIdsJson { get; set; } = string.Empty;
    public string LeaseToken { get; set; } = string.Empty;
    public long LeaseExpiresAtUtcTicks { get; set; }
    public Guid ConcurrencyToken { get; set; }
}

internal sealed class JobRecurringCursorEntity
{
    public string SchedulerScopeKey { get; set; } = string.Empty;
    public long ActivationEpoch { get; set; }
    public string JobRevisionId { get; set; } = string.Empty;
    public string TemplateJson { get; set; } = string.Empty;
    public string ScheduleJson { get; set; } = string.Empty;
    public long? NextOccurrenceUtcTicks { get; set; }
    public long CursorVersion { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
    public bool IsSuspended { get; set; }
    public long LastSynchronizedChangeEpoch { get; set; }
    public Guid ConcurrencyToken { get; set; }
}
