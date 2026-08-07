using System.Collections.Immutable;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Services.Support;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores module composition profiling data for one Monica application instance.
/// </summary>
internal sealed class ModuleProfilingState
{
    /// <summary>
    /// Gets active system phases keyed by profiler name.
    /// </summary>
    internal Dictionary<string, ModuleSystemPhaseProfileStart> ActiveSystemPhases { get; } = [];

    /// <summary>
    /// Gets completed system-phase executions in occurrence order.
    /// </summary>
    internal List<ModuleSystemPhasePerformanceInfo> SystemPhases { get; } = [];

    /// <summary>
    /// Gets per-module profiling data keyed by module type.
    /// </summary>
    internal Dictionary<Type, ModuleProfileState> ModuleProfiles { get; } = [];

    /// <summary>
    /// Gets lifecycle milestones in occurrence order.
    /// </summary>
    internal List<ModuleCompositionMilestonePerformanceInfo> Milestones { get; } = [];

    /// <summary>
    /// Gets the latest bounded statistics produced by the type-discovery compiler and commit pipeline.
    /// </summary>
    internal TypeDiscoveryStatistics TypeDiscoveryStatistics { get; set; } = new();

    /// <summary>
    /// Gets reflection-free summaries for structurally distinct discovery queries.
    /// </summary>
    internal ImmutableArray<TypeDiscoveryQuerySummary> TypeDiscoveryQueries { get; set; } = [];

    /// <summary>
    /// Gets the UTC timestamp paired with <see cref="OriginTimestamp"/>.
    /// </summary>
    internal DateTimeOffset OriginUtc { get; set; }

    /// <summary>
    /// Gets the monotonic timestamp used as offset zero.
    /// </summary>
    internal long? OriginTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the terminal monotonic timestamp after composition profiling stops.
    /// </summary>
    internal long? TerminalTimestamp { get; set; }

    /// <summary>
    /// Gets or sets whether system-level module profiling is currently running.
    /// </summary>
    internal bool IsStarted { get; set; }

    /// <summary>
    /// Gets the next global occurrence sequence for milestones and serial phases.
    /// </summary>
    internal long NextSequence { get; set; }

    /// <summary>
    /// Clears profiling data.
    /// </summary>
    internal void Clear()
    {
        ActiveSystemPhases.Clear();
        SystemPhases.Clear();
        ModuleProfiles.Clear();
        Milestones.Clear();
        TypeDiscoveryStatistics = new TypeDiscoveryStatistics();
        TypeDiscoveryQueries = [];
        OriginUtc = default;
        OriginTimestamp = null;
        TerminalTimestamp = null;
        IsStarted = false;
        NextSequence = 0;
    }
}

/// <summary>
/// Stores the active boundary of one system-level serial phase.
/// </summary>
internal sealed record ModuleSystemPhaseProfileStart(
    long Sequence,
    long StartedTimestamp,
    DateTimeOffset StartedAtUtc,
    ModuleSystemStage? Stage);
