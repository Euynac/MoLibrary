using System.Collections.Immutable;
using System.Diagnostics;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.State;

/// <summary>
/// Stores repeated serial callback executions for one module.
/// </summary>
internal sealed class ModuleProfileState(Type moduleType, ModuleKey moduleKey, int registrationOrder)
{
    private readonly Dictionary<ModulePhase, ModulePhaseProfileStart> _activePhases = [];
    private readonly List<ModulePhaseProfileExecution> _executions = [];

    /// <summary>
    /// Gets the profiled module type.
    /// </summary>
    internal Type ModuleType { get; } = moduleType;

    /// <summary>
    /// Gets the stable module key captured from the registration graph.
    /// </summary>
    internal ModuleKey ModuleKey { get; } = moduleKey;

    /// <summary>
    /// Gets the latest dependency-aware registration order observed for the module.
    /// </summary>
    internal int RegistrationOrder { get; private set; } = registrationOrder;

    /// <summary>
    /// Refreshes mutable registration metadata after dependency ordering is finalized.
    /// </summary>
    internal void UpdateRegistrationOrder(int registrationOrder)
    {
        RegistrationOrder = registrationOrder;
    }

    /// <summary>
    /// Starts one callback occurrence while rejecting overlapping executions of the same module phase.
    /// </summary>
    internal void StartPhase(
        ModulePhase phase,
        ModuleCallbackKind kind,
        string? workItemId,
        long sequence,
        long startedTimestamp,
        DateTimeOffset startedAtUtc)
    {
        if (!_activePhases.TryAdd(
                phase,
                new ModulePhaseProfileStart(sequence, kind, workItemId, startedTimestamp, startedAtUtc)))
        {
            throw new InvalidOperationException(
                $"Module {ModuleType.FullName} phase {phase} is already running.");
        }
    }

    /// <summary>
    /// Completes one active callback occurrence and retains it independently from repeated executions.
    /// </summary>
    internal double StopPhase(
        ModulePhase phase,
        long completedTimestamp,
        DateTimeOffset completedAtUtc)
    {
        if (!_activePhases.Remove(phase, out var start))
        {
            return 0;
        }

        var execution = new ModulePhaseProfileExecution(
            start.Sequence,
            phase,
            start.Kind,
            start.WorkItemId,
            start.StartedTimestamp,
            completedTimestamp,
            start.StartedAtUtc,
            completedAtUtc);
        _executions.Add(execution);
        return GetDurationMs(execution);
    }

    /// <summary>
    /// Gets the aggregate duration of all completed callbacks for this module.
    /// </summary>
    internal double GetSerialPhaseDurationMs()
    {
        return _executions.Sum(GetDurationMs);
    }

    /// <summary>
    /// Projects internal monotonic boundaries into the public diagnostics contract.
    /// </summary>
    internal IReadOnlyList<ModulePhaseExecutionPerformanceInfo> CreateExecutions(Func<long, double> getOffsetMs)
    {
        return CreateDiagnosticsCapture().CreateExecutions(getOffsetMs);
    }

    /// <summary>Copies raw callback boundaries for projection after the profiler lock is released.</summary>
    internal ModuleProfileDiagnosticsCapture CreateDiagnosticsCapture()
    {
        return new ModuleProfileDiagnosticsCapture(
            ModuleType,
            ModuleKey,
            RegistrationOrder,
            _executions.ToImmutableArray());
    }

    internal sealed record ModuleProfileDiagnosticsCapture(
        Type ModuleType,
        ModuleKey ModuleKey,
        int RegistrationOrder,
        ImmutableArray<ModulePhaseProfileExecution> Executions)
    {
        /// <summary>Projects copied monotonic boundaries into diagnostic callback records.</summary>
        internal IReadOnlyList<ModulePhaseExecutionPerformanceInfo> CreateExecutions(
            Func<long, double> getOffsetMs)
        {
            return Executions
            .OrderBy(static execution => execution.Sequence)
            .Select(execution => new ModulePhaseExecutionPerformanceInfo
            {
                ExecutionId = $"module-phase-{execution.Sequence:D6}",
                Sequence = execution.Sequence,
                ModuleKey = this.ModuleKey,
                ModuleTypeName = this.ModuleType.Name,
                ModuleFullTypeName = this.ModuleType.FullName ?? this.ModuleType.Name,
                ModuleRegistrationOrder = this.RegistrationOrder,
                Phase = execution.Phase,
                Kind = execution.Kind,
                WorkItemId = execution.WorkItemId,
                StartedAtUtc = execution.StartedAtUtc,
                CompletedAtUtc = execution.CompletedAtUtc,
                StartedOffsetMs = getOffsetMs(execution.StartedTimestamp),
                CompletedOffsetMs = getOffsetMs(execution.CompletedTimestamp)
            })
            .ToArray();
        }
    }

    private static double GetDurationMs(ModulePhaseProfileExecution execution)
    {
        return Stopwatch.GetElapsedTime(execution.StartedTimestamp, execution.CompletedTimestamp)
            .TotalMilliseconds;
    }
}

/// <summary>
/// Stores an active module callback boundary.
/// </summary>
internal sealed record ModulePhaseProfileStart(
    long Sequence,
    ModuleCallbackKind Kind,
    string? WorkItemId,
    long StartedTimestamp,
    DateTimeOffset StartedAtUtc);

/// <summary>
/// Stores one completed module callback before owner metadata is projected.
/// </summary>
internal sealed record ModulePhaseProfileExecution(
    long Sequence,
    ModulePhase Phase,
    ModuleCallbackKind Kind,
    string? WorkItemId,
    long StartedTimestamp,
    long CompletedTimestamp,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);
