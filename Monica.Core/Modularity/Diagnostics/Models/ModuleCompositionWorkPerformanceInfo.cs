using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Describes one isolated composition work item scheduled by a module.
/// </summary>
public sealed class ModuleCompositionWorkPerformanceInfo
{
    /// <summary>
    /// Gets the stable work name within the owning module.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the serial module phase that scheduled the work.
    /// </summary>
    public required ModulePhase OriginPhase { get; init; }

    /// <summary>
    /// Gets the latest composition checkpoint by which the work had to complete.
    /// </summary>
    public required ModuleCompositionWorkDeadline Deadline { get; init; }

    /// <summary>
    /// Gets the terminal work status.
    /// </summary>
    public required ModuleCompositionWorkStatus Status { get; init; }

    /// <summary>
    /// Gets when the module submitted the work.
    /// </summary>
    public required DateTimeOffset SubmittedAtUtc { get; init; }

    /// <summary>
    /// Gets when a composition worker began executing the work.
    /// </summary>
    public required DateTimeOffset StartedAtUtc { get; init; }

    /// <summary>
    /// Gets when execution stopped.
    /// </summary>
    public required DateTimeOffset CompletedAtUtc { get; init; }

    /// <summary>
    /// Gets how long the work waited for a composition worker, in milliseconds.
    /// </summary>
    public required long QueueDurationMs { get; init; }

    /// <summary>
    /// Gets active worker execution time, in milliseconds.
    /// </summary>
    public required long ExecutionDurationMs { get; init; }

    /// <summary>
    /// Gets the recursive exception message when execution failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Defines terminal states for required module composition work.
/// </summary>
public enum ModuleCompositionWorkStatus
{
    /// <summary>
    /// The work completed successfully.
    /// </summary>
    Succeeded,

    /// <summary>
    /// The work failed and Monica aborted composition at its checkpoint.
    /// </summary>
    Failed
}

/// <summary>
/// Describes the serial wait imposed at one composition-work checkpoint.
/// </summary>
public sealed class ModuleCompositionCheckpointPerformanceInfo
{
    /// <summary>
    /// Gets the checkpoint reached by the serial composition pipeline.
    /// </summary>
    public required ModuleCompositionWorkDeadline Deadline { get; init; }

    /// <summary>
    /// Gets the number of previously unreported work items due at this checkpoint.
    /// </summary>
    public required int WorkItemCount { get; init; }

    /// <summary>
    /// Gets how long the serial composition thread waited, in milliseconds.
    /// </summary>
    public required long WaitDurationMs { get; init; }
}
