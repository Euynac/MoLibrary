using System.Collections.Immutable;

namespace Monica.Framework.Seeder.Models;

/// <summary>
/// Provides an immutable point-in-time view of the host's complete seeder run.
/// </summary>
public sealed record SeederStateSnapshot
{
    /// <summary>Gets when this snapshot was captured.</summary>
    public required DateTimeOffset CapturedAtUtc { get; init; }

    /// <summary>Gets the current lifecycle state of the complete run.</summary>
    public required SeederRunStatus Status { get; init; }

    /// <summary>Gets when DAG scheduling began after application startup, or <see langword="null"/> beforehand.</summary>
    public DateTimeOffset? StartedAtUtc { get; init; }

    /// <summary>Gets when every seeder reached a terminal state, or <see langword="null"/> while work remains.</summary>
    public DateTimeOffset? CompletedAtUtc { get; init; }

    /// <summary>Gets the elapsed run duration at snapshot capture time, or <see langword="null"/> before scheduling begins.</summary>
    public TimeSpan? Duration { get; init; }

    /// <summary>Gets the full type name of the deterministic fail-fast trigger while aborting or after abort.</summary>
    public string? FailFastTriggerSeederTypeName { get; init; }

    /// <summary>Gets the discovered seeders ordered by their full type names.</summary>
    public ImmutableArray<SeederExecutionSnapshot> Seeders { get; init; } = [];

    /// <summary>Gets the total number of discovered seeders.</summary>
    public int TotalCount => Seeders.Length;

    /// <summary>Gets the number of seeders waiting to run.</summary>
    public int PendingCount => Count(SeederStatus.Pending);

    /// <summary>Gets the number of seeders currently running.</summary>
    public int RunningCount => Count(SeederStatus.Running);

    /// <summary>Gets the number of successful seeders.</summary>
    public int SucceededCount => Count(SeederStatus.Succeeded);

    /// <summary>Gets the number of failed seeders.</summary>
    public int FailedCount => Count(SeederStatus.Failed);

    /// <summary>Gets the number of dependency-blocked seeders.</summary>
    public int BlockedCount => Count(SeederStatus.Blocked);

    /// <summary>Gets the number of cancelled seeders.</summary>
    public int CancelledCount => Count(SeederStatus.Cancelled);

    /// <summary>Gets the number of seeders in a terminal state.</summary>
    public int CompletedCount => SucceededCount + FailedCount + BlockedCount + CancelledCount;

    /// <summary>Gets the number of required seeders that have not succeeded.</summary>
    public int RequiredUnsuccessfulCount => Seeders.Count(static seeder =>
        seeder.Criticality == SeederCriticality.Required && seeder.Status != SeederStatus.Succeeded);

    /// <summary>Gets the number of optional seeders that ended unsuccessfully.</summary>
    public int OptionalUnsuccessfulCount => Seeders.Count(static seeder =>
        seeder.Criticality == SeederCriticality.Optional &&
        seeder.Status is SeederStatus.Failed or SeederStatus.Blocked or SeederStatus.Cancelled);

    /// <summary>Gets the current Seeder contribution to application readiness.</summary>
    public SeederReadinessStatus ReadinessStatus
    {
        get
        {
            if (Status is SeederRunStatus.Aborting or SeederRunStatus.Aborted || RequiredUnsuccessfulCount > 0)
            {
                return SeederReadinessStatus.Unhealthy;
            }

            return OptionalUnsuccessfulCount > 0
                ? SeederReadinessStatus.Degraded
                : SeederReadinessStatus.Healthy;
        }
    }

    /// <summary>Gets whether the scheduler has reached a terminal run state.</summary>
    public bool IsCompleted => Status is SeederRunStatus.Succeeded or
        SeederRunStatus.CompletedWithFailures or
        SeederRunStatus.Aborted or
        SeederRunStatus.Cancelled;

    private int Count(SeederStatus status) => Seeders.Count(seeder => seeder.Status == status);
}
