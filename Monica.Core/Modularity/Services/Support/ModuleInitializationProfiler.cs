using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using Monica.Core.Extensions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.State;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Records one monotonic, host-owned timeline for Monica module composition.
/// </summary>
internal sealed class ModuleInitializationProfiler
{
    private readonly object _gate = new();
    private readonly ModuleProfilingState _state = new();
    private Func<ModuleStartupWorkSnapshot>? _startupWorkSnapshotProvider;
    private long _lastStartupWorkRevision = -1;
    private long _revision;

    /// <summary>
    /// Gets whether the full module-composition stopwatch is currently running.
    /// </summary>
    internal bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _state.IsStarted;
            }
        }
    }

    /// <summary>
    /// Clears all profiling data for this host.
    /// </summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _startupWorkSnapshotProvider = null;
            _lastStartupWorkRevision = -1;
            _state.Clear();
            _revision++;
        }
    }

    /// <summary>
    /// Starts the module composition timeline at the <c>AddMonica(...)</c> boundary.
    /// </summary>
    internal void StartModuleSystem()
    {
        lock (_gate)
        {
            if (_state.IsStarted)
            {
                return;
            }

            _state.OriginUtc = DateTimeOffset.UtcNow;
            _state.OriginTimestamp = Stopwatch.GetTimestamp();
            _state.TerminalTimestamp = null;
            _state.IsStarted = true;
            _revision++;
            RecordMilestone(ModuleCompositionMilestone.CompositionStarted);
        }
    }

    /// <summary>
    /// Stops the end-to-end composition timer without inventing a successful completion milestone.
    /// </summary>
    internal void StopModuleSystem()
    {
        lock (_gate)
        {
            if (!_state.IsStarted)
            {
                return;
            }

            _state.TerminalTimestamp = Stopwatch.GetTimestamp();
            _state.IsStarted = false;
            _revision++;
        }
    }

    /// <summary>
    /// Records one unique host-owned lifecycle milestone.
    /// </summary>
    /// <param name="milestone">The milestone that just occurred.</param>
    internal void RecordMilestone(ModuleCompositionMilestone milestone)
    {
        lock (_gate)
        {
            if (_state.OriginTimestamp is not { } originTimestamp)
            {
                throw new InvalidOperationException("Module composition profiling has not started.");
            }

            if (_state.Milestones.Any(info => info.Milestone == milestone))
            {
                throw new InvalidOperationException($"Composition milestone {milestone} has already been recorded.");
            }

            var occurredAtUtc = DateTimeOffset.UtcNow;
            var timestamp = milestone == ModuleCompositionMilestone.CompositionStarted
                ? originTimestamp
                : Stopwatch.GetTimestamp();
            _state.Milestones.Add(new ModuleCompositionMilestonePerformanceInfo
            {
                Milestone = milestone,
                Sequence = NextSequence(),
                OccurredAtUtc = milestone == ModuleCompositionMilestone.CompositionStarted
                    ? _state.OriginUtc
                    : occurredAtUtc,
                OffsetMs = GetOffsetMs(timestamp)
            });
            _revision++;
        }
    }

    /// <summary>
    /// Starts one system-level serial phase execution.
    /// </summary>
    /// <param name="phaseName">The profiler phase name.</param>
    internal void StartPhase(string phaseName)
    {
        StartPhaseCore(phaseName, stage: null);
    }

    /// <summary>
    /// Starts one strongly typed framework orchestration stage.
    /// </summary>
    internal void StartStage(ModuleSystemStage stage)
    {
        StartPhaseCore(stage.ToString(), stage);
    }

    private void StartPhaseCore(string phaseName, ModuleSystemStage? stage)
    {
        lock (_gate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(phaseName);
            if (_state.ActiveSystemPhases.ContainsKey(phaseName))
            {
                throw new InvalidOperationException($"System composition phase '{phaseName}' is already running.");
            }

            _state.ActiveSystemPhases.Add(
                phaseName,
                new ModuleSystemPhaseProfileStart(
                    NextSequence(),
                    Stopwatch.GetTimestamp(),
                    DateTimeOffset.UtcNow,
                    stage));
            _revision++;
        }
    }

    /// <summary>
    /// Completes one system-level serial phase execution.
    /// </summary>
    /// <param name="phaseName">The profiler phase name.</param>
    /// <returns>The completed phase duration in whole milliseconds.</returns>
    internal long StopPhase(string phaseName)
    {
        lock (_gate)
        {
            if (!_state.ActiveSystemPhases.Remove(phaseName, out var start))
            {
                return 0;
            }

            var completedTimestamp = Stopwatch.GetTimestamp();
            var execution = new ModuleSystemPhasePerformanceInfo
            {
                ExecutionId = FormatExecutionId("system-phase", start.Sequence),
                Sequence = start.Sequence,
                PhaseName = phaseName,
                Stage = start.Stage,
                StartedAtUtc = start.StartedAtUtc,
                CompletedAtUtc = DateTimeOffset.UtcNow,
                StartedOffsetMs = GetOffsetMs(start.StartedTimestamp),
                CompletedOffsetMs = GetOffsetMs(completedTimestamp)
            };
            _state.SystemPhases.Add(execution);
            _revision++;
            return ToWholeMilliseconds(execution.DurationMs);
        }
    }

    /// <summary>
    /// Completes one strongly typed framework orchestration stage.
    /// </summary>
    internal long StopStage(ModuleSystemStage stage)
    {
        return StopPhase(stage.ToString());
    }

    /// <summary>
    /// Starts one serial callback execution for a module phase.
    /// </summary>
    internal void StartModulePhase(
        Type moduleType,
        ModuleKey moduleKey,
        int registrationOrder,
        ModulePhase phase,
        ModuleCallbackKind kind = ModuleCallbackKind.Lifecycle,
        string? workItemId = null)
    {
        lock (_gate)
        {
            ArgumentNullException.ThrowIfNull(moduleType);
            GetOrCreateModuleProfile(moduleType, moduleKey, registrationOrder).StartPhase(
                phase,
                kind,
                workItemId,
                NextSequence(),
                Stopwatch.GetTimestamp(),
                DateTimeOffset.UtcNow);
            _revision++;
        }
    }

    /// <summary>
    /// Completes one serial callback execution for a module phase.
    /// </summary>
    internal long StopModulePhase(Type moduleType, ModulePhase phase)
    {
        lock (_gate)
        {
            if (!_state.ModuleProfiles.TryGetValue(moduleType, out var profile))
            {
                return 0;
            }

            var duration = profile.StopPhase(phase, Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow);
            _revision++;
            return ToWholeMilliseconds(duration);
        }
    }

    /// <summary>
    /// Records compiler-owned counters and reflection-free query summaries.
    /// </summary>
    internal void RecordTypeDiscoveryCompilation(
        TypeDiscoveryStatistics statistics,
        IReadOnlyList<TypeDiscoveryQuerySummary> queries)
    {
        lock (_gate)
        {
            ArgumentNullException.ThrowIfNull(statistics);
            ArgumentNullException.ThrowIfNull(queries);
            _state.TypeDiscoveryStatistics = statistics;
            _state.TypeDiscoveryQueries = queries.ToImmutableArray();
            _revision++;
        }
    }

    /// <summary>
    /// Completes type-discovery counters with serial commit and service-writer outcomes.
    /// </summary>
    internal void RecordTypeDiscoveryCommit(
        int commitCallbackCount,
        TypeDiscoveryServiceRegistrationStatistics serviceRegistrations)
    {
        lock (_gate)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(commitCallbackCount);
            ArgumentNullException.ThrowIfNull(serviceRegistrations);
            _state.TypeDiscoveryStatistics = _state.TypeDiscoveryStatistics with
            {
                CommitCallbackCount = commitCallbackCount,
                ServiceRegistrations = serviceRegistrations
            };
            _revision++;
        }
    }

    /// <summary>
    /// Gets the latest immutable type-discovery counter snapshot.
    /// </summary>
    internal TypeDiscoveryStatistics GetTypeDiscoveryStatistics()
    {
        lock (_gate)
        {
            return _state.TypeDiscoveryStatistics;
        }
    }

    /// <summary>
    /// Gets reflection-free discovery query summaries in stable compiler order.
    /// </summary>
    internal IReadOnlyList<TypeDiscoveryQuerySummary> GetTypeDiscoveryQueries()
    {
        lock (_gate)
        {
            return _state.TypeDiscoveryQueries;
        }
    }

    /// <summary>
    /// Attaches the host-owned live startup-work diagnostic source.
    /// </summary>
    internal void AttachStartupWorkDiagnostics(Func<ModuleStartupWorkSnapshot> snapshotProvider)
    {
        lock (_gate)
        {
            ArgumentNullException.ThrowIfNull(snapshotProvider);
            if (_startupWorkSnapshotProvider is not null)
            {
                throw new InvalidOperationException("Startup-work diagnostics are already attached to this host.");
            }

            _startupWorkSnapshotProvider = snapshotProvider;
            _revision++;
        }
    }

    /// <summary>
    /// Advances the diagnostic revision after the attached startup scheduler changes visible state.
    /// </summary>
    internal void RecordExternalMutation()
    {
        lock (_gate)
        {
            _revision++;
        }
    }

    /// <summary>
    /// Captures one revision-consistent profiler snapshot under a short host-local lock.
    /// </summary>
    internal ModuleProfilingDiagnosticsCapture CaptureDiagnostics()
    {
        while (true)
        {
            ModuleProfilerStateCapture state;
            lock (_gate)
            {
                if (_state.IsStarted)
                {
                    // Live durations advance between lifecycle mutations, so every live observation gets a revision.
                    _revision++;
                }

                state = new ModuleProfilerStateCapture(
                    _revision,
                    _state.OriginUtc,
                    _state.OriginTimestamp,
                    _state.TerminalTimestamp,
                    _state.IsStarted,
                    _state.Milestones.ToImmutableArray(),
                    _state.SystemPhases.ToImmutableArray(),
                    _state.ModuleProfiles.Values
                        .Select(static profile => profile.CreateDiagnosticsCapture())
                        .ToImmutableArray(),
                    _state.TypeDiscoveryStatistics,
                    _state.TypeDiscoveryQueries.ToImmutableArray(),
                    _startupWorkSnapshotProvider);
            }

            // Never invoke the scheduler while holding the profiler lock. The scheduler reports mutations back into
            // the profiler, so keeping this call outside also removes the former lock-order inversion.
            var startupWork = state.StartupWorkSnapshotProvider?.Invoke()
                              ?? new ModuleStartupWorkSnapshot(0, [], []);
            lock (_gate)
            {
                if (_revision != state.Revision)
                {
                    continue;
                }

                if (_lastStartupWorkRevision != startupWork.Revision)
                {
                    _lastStartupWorkRevision = startupWork.Revision;
                    _revision++;
                    state = state with { Revision = _revision };
                }

                if (!state.IsStarted && startupWork.WorkItems.Any(static work => !work.IsTerminal))
                {
                    _revision++;
                    state = state with { Revision = _revision };
                }
            }

            var observedTimestamp = Stopwatch.GetTimestamp();
            var observedAtUtc = DateTimeOffset.UtcNow;
            var performance = CreateCompositionPerformance(
                state,
                startupWork,
                observedTimestamp,
                observedAtUtc);
            return new ModuleProfilingDiagnosticsCapture(
                state.Revision,
                performance,
                state.TypeDiscoveryStatistics,
                state.TypeDiscoveryQueries);
        }
    }

    /// <summary>Gets the current revision without projecting diagnostic state.</summary>
    internal long GetRevision()
    {
        lock (_gate)
        {
            return _revision;
        }
    }

    /// <summary>
    /// Builds the immutable end-to-end composition performance model.
    /// </summary>
    internal ModuleCompositionPerformance GetCompositionPerformance()
    {
        return CaptureDiagnostics().Performance;
    }

    private static ModuleCompositionPerformance CreateCompositionPerformance(
        ModuleProfilerStateCapture state,
        ModuleStartupWorkSnapshot startupWorkSnapshot,
        long observedTimestamp,
        DateTimeOffset observedAtUtc)
    {
        double GetCapturedOffsetMs(long timestamp) => GetOffsetMs(state.OriginTimestamp, timestamp);

        var moduleExecutions = state.ModuleProfiles
            .SelectMany(profile => profile.CreateExecutions(GetCapturedOffsetMs))
            .OrderBy(static execution => execution.Sequence)
            .ToArray();
        var startupWork = CreateStartupWorkDiagnostics(startupWorkSnapshot, state.OriginTimestamp);

        return new ModuleCompositionPerformance
        {
            StartedAtUtc = state.OriginUtc,
            ElapsedDurationMs = GetElapsedDurationMs(
                state.OriginTimestamp,
                state.TerminalTimestamp,
                observedTimestamp),
            ObservedAtUtc = observedAtUtc,
            ObservedDurationMs = GetOffsetMs(state.OriginTimestamp, observedTimestamp),
            Milestones = state.Milestones.OrderBy(static milestone => milestone.Sequence).ToArray(),
            SystemPhases = state.SystemPhases.OrderBy(static phase => phase.Sequence).ToArray(),
            ModulePhaseExecutions = moduleExecutions,
            StartupWorkItems = startupWork.WorkItems,
            StartupWorkBarriers = startupWork.Barriers
        };
    }

    /// <summary>
    /// Creates the module-oriented performance projection used by details and tables.
    /// </summary>
    internal ModulePerformanceInfo GetModulePerformance(ModuleRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var performance = CaptureDiagnostics().Performance;
        return new ModulePerformanceInfo
        {
            ModuleKey = snapshot.ModuleKey,
            ModuleTypeName = snapshot.ModuleType.Name,
            ModuleFullTypeName = snapshot.ModuleType.FullName ?? snapshot.ModuleType.Name,
            RegistrationOrder = snapshot.RegisterInfo.Order,
            IsRuntimeAvailable = true,
            PhaseExecutions = performance.ModulePhaseExecutions
                .Where(execution => execution.ModuleKey == snapshot.ModuleKey)
                .ToArray(),
            StartupWorkItems = performance.StartupWorkItems
                .Where(work => work.ModuleKey == snapshot.ModuleKey)
                .OrderBy(static work => work.Sequence)
                .ToArray()
        };
    }

    /// <summary>
    /// Creates module-oriented projections for every registration that executed a callback, including disabled modules.
    /// </summary>
    internal IReadOnlyList<ModulePerformanceInfo> GetModulePerformances(
        IReadOnlySet<ModuleKey> runtimeModuleKeys)
    {
        ArgumentNullException.ThrowIfNull(runtimeModuleKeys);
        var performance = CaptureDiagnostics().Performance;
        return performance.ModulePhaseExecutions
            .GroupBy(static execution => execution.ModuleKey)
            .OrderBy(static group => group.Min(execution => execution.ModuleRegistrationOrder))
            .ThenBy(static group => group.First().ModuleFullTypeName, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new ModulePerformanceInfo
                {
                    ModuleKey = group.Key,
                    ModuleTypeName = first.ModuleTypeName,
                    ModuleFullTypeName = first.ModuleFullTypeName,
                    RegistrationOrder = first.ModuleRegistrationOrder,
                    IsRuntimeAvailable = runtimeModuleKeys.Contains(group.Key),
                    PhaseExecutions = group.ToArray(),
                    StartupWorkItems = performance.StartupWorkItems
                        .Where(work => work.ModuleKey == group.Key)
                        .OrderBy(static work => work.Sequence)
                        .ToArray()
                };
            })
            .ToArray();
    }

    /// <summary>
    /// Gets module profiles sorted by aggregate serial callback duration.
    /// </summary>
    internal List<ModuleProfileState> GetModuleProfilesSortedBySerialPhaseDuration()
    {
        ModuleProfileState[] profiles;
        lock (_gate)
        {
            profiles = _state.ModuleProfiles.Values.ToArray();
        }

        return profiles
            .OrderByDescending(static profile => profile.GetSerialPhaseDurationMs())
            .ToList();
    }

    /// <summary>
    /// Gets a concise factual summary without treating overlapping timing dimensions as additive.
    /// </summary>
    internal string GetPerformanceSummary()
    {
        var composition = CaptureDiagnostics().Performance;
        var initialization = composition.Initialization;
        var serviceRegistration = composition.ServiceRegistration;
        var builder = new StringBuilder();
        builder.AppendLine("Module System Performance Summary:");
        builder.AppendLine($"Module system initialization elapsed: {initialization.TotalDurationMs:F1}ms");
        builder.AppendLine($"  Monica framework work: {initialization.MonicaFrameworkDurationMs:F1}ms");
        builder.AppendLine($"  Application module configuration: {initialization.ApplicationConfigurationDurationMs:F1}ms");
        builder.AppendLine($"  Host-owned gaps: {initialization.HostOwnedDurationMs:F1}ms");
        builder.AppendLine($"Service registration elapsed: {serviceRegistration.TotalDurationMs:F1}ms");
        builder.AppendLine($"  Application module configuration: {serviceRegistration.ApplicationConfigurationDurationMs:F1}ms");
        builder.AppendLine($"  Serial module callbacks: {serviceRegistration.SerialModuleCallbackDurationMs:F1}ms");
        builder.AppendLine($"  Blocking startup barriers: {serviceRegistration.BlockingWaitDurationMs:F1}ms");
        builder.AppendLine($"  Monica orchestration: {serviceRegistration.OrchestrationDurationMs:F1}ms");
        builder.AppendLine($"Aggregate system phases (overlapping diagnostic dimension): {composition.AggregateSystemPhaseDurationMs:F1}ms");

        if (composition.StartupWorkItems.Count > 0)
        {
            builder.AppendLine("Parallel work (overlaps the exact elapsed partitions above):");
            builder.AppendLine($"  Active span: {composition.ParallelWorkActiveSpanMs:F1}ms");
            builder.AppendLine($"  Aggregate worker execution: {composition.AggregateWorkExecutionDurationMs:F1}ms");
            builder.AppendLine($"  Aggregate queue duration: {composition.AggregateWorkQueueDurationMs:F1}ms");
        }

        builder.AppendLine("Slowest serial module callbacks:");
        foreach (var module in composition.ModulePhaseExecutions
                     .GroupBy(static execution => execution.ModuleKey)
                     .Select(static group => new
                     {
                         Name = group.First().ModuleTypeName,
                         DurationMs = group.Sum(static execution => execution.DurationMs)
                     })
                     .OrderByDescending(static module => module.DurationMs)
                     .Take(5))
        {
            builder.AppendLine($"  {module.Name}: {module.DurationMs:F1}ms");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets aggregate serial callback time for one module in whole milliseconds.
    /// </summary>
    internal long GetModuleSerialPhaseDuration(Type moduleType)
    {
        lock (_gate)
        {
            return _state.ModuleProfiles.TryGetValue(moduleType, out var profile)
                ? ToWholeMilliseconds(profile.GetSerialPhaseDurationMs())
                : 0;
        }
    }

    private static StartupWorkDiagnostics CreateStartupWorkDiagnostics(
        ModuleStartupWorkSnapshot snapshot,
        long? originTimestamp)
    {
        var barriers = snapshot.Barriers.Select(barrier =>
            new ModuleStartupWorkBarrierPerformanceInfo
            {
                Sequence = barrier.Sequence,
                Barrier = barrier.Barrier,
                EnteredAtUtc = barrier.EnteredAtUtc,
                ReleasedAtUtc = barrier.ReleasedAtUtc,
                EnteredOffsetMs = GetOffsetMs(originTimestamp, barrier.EnteredTimestamp),
                ReleasedOffsetMs = GetOffsetMs(originTimestamp, barrier.ReleasedTimestamp),
                DueWorkItemIds = barrier.WorkItems.Select(static work => work.WorkItemId).ToArray(),
                PendingWorkItems = barrier.PendingWorkItems.Select(static pending =>
                    new ModuleStartupWorkBarrierPendingWorkInfo
                    {
                        WorkItemId = pending.WorkItemId,
                        RemainingDurationMs = pending.RemainingDuration.TotalMilliseconds
                    }).ToArray(),
                ReleasingWorkItemId = barrier.ReleasingWorkItemId
            }).ToArray();
        var barrierByWorkItemId = barriers
            .SelectMany(barrier => barrier.DueWorkItemIds.Select(workItemId => (workItemId, barrier)))
            .ToDictionary(static pair => pair.workItemId, static pair => pair.barrier, StringComparer.Ordinal);
        var workItems = snapshot.WorkItems.Select(result =>
        {
            barrierByWorkItemId.TryGetValue(result.WorkItemId, out var barrier);
            var pending = barrier?.PendingWorkItems.FirstOrDefault(item =>
                string.Equals(item.WorkItemId, result.WorkItemId, StringComparison.Ordinal));
            return new ModuleStartupWorkPerformanceInfo
            {
                WorkItemId = result.WorkItemId,
                Sequence = result.Sequence,
                ModuleKey = result.ModuleKey,
                ModuleTypeName = result.ModuleType.Name,
                ModuleFullTypeName = result.ModuleType.FullName ?? result.ModuleType.Name,
                ModuleRegistrationOrder = result.RegistrationOrder,
                Name = result.Name,
                OriginPhase = result.OriginPhase,
                Barrier = result.Barrier,
                Status = result.Status switch
                {
                    ModuleStartupWorkExecutionStatus.Queued => ModuleStartupWorkStatus.Queued,
                    ModuleStartupWorkExecutionStatus.Running => ModuleStartupWorkStatus.Running,
                    ModuleStartupWorkExecutionStatus.Succeeded => ModuleStartupWorkStatus.Succeeded,
                    ModuleStartupWorkExecutionStatus.Failed => ModuleStartupWorkStatus.Failed,
                    _ => throw new ArgumentOutOfRangeException(nameof(result.Status), result.Status, null)
                },
                SubmittedAtUtc = result.SubmittedAtUtc,
                StartedAtUtc = result.StartedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                SubmittedOffsetMs = GetOffsetMs(originTimestamp, result.SubmittedTimestamp),
                StartedOffsetMs = result.StartedTimestamp is { } startedTimestamp
                    ? GetOffsetMs(originTimestamp, startedTimestamp)
                    : null,
                CompletedOffsetMs = result.CompletedTimestamp is { } completedTimestamp
                    ? GetOffsetMs(originTimestamp, completedTimestamp)
                    : null,
                WasPendingAtBarrier = pending is not null,
                RemainingAtBarrierMs = pending?.RemainingDurationMs ?? 0,
                IsBarrierReleaser = string.Equals(
                    barrier?.ReleasingWorkItemId,
                    result.WorkItemId,
                    StringComparison.Ordinal),
                QueueDurationMs = result.QueueDuration.TotalMilliseconds,
                ExecutionDurationMs = result.ExecutionDuration.TotalMilliseconds,
                ErrorMessage = result.Failure?.GetMessageRecursively()
            };
        }).ToArray();

        return new StartupWorkDiagnostics(workItems, barriers);
    }

    private ModuleProfileState GetOrCreateModuleProfile(
        Type moduleType,
        ModuleKey moduleKey,
        int registrationOrder)
    {
        if (_state.ModuleProfiles.TryGetValue(moduleType, out var profile))
        {
            profile.UpdateRegistrationOrder(registrationOrder);
            return profile;
        }

        profile = new ModuleProfileState(moduleType, moduleKey, registrationOrder);
        _state.ModuleProfiles.Add(moduleType, profile);
        return profile;
    }

    private long NextSequence()
    {
        return _state.NextSequence++;
    }

    private double GetOffsetMs(long timestamp)
    {
        return GetOffsetMs(_state.OriginTimestamp, timestamp);
    }

    private static double GetOffsetMs(long? originTimestamp, long timestamp)
    {
        if (originTimestamp is not { } origin || timestamp <= origin)
        {
            return 0;
        }

        return Stopwatch.GetElapsedTime(origin, timestamp).TotalMilliseconds;
    }

    private double GetElapsedDurationMs(long? observedTimestamp = null)
    {
        return GetElapsedDurationMs(
            _state.OriginTimestamp,
            _state.TerminalTimestamp,
            observedTimestamp ?? Stopwatch.GetTimestamp());
    }

    private static double GetElapsedDurationMs(
        long? originTimestamp,
        long? terminalTimestamp,
        long observedTimestamp)
    {
        if (originTimestamp is not { } origin)
        {
            return 0;
        }

        return Stopwatch.GetElapsedTime(origin, terminalTimestamp ?? observedTimestamp).TotalMilliseconds;
    }

    private static string FormatExecutionId(string prefix, long sequence)
    {
        return $"{prefix}-{sequence:D6}";
    }

    private static long ToWholeMilliseconds(double milliseconds)
    {
        return (long)Math.Floor(Math.Max(0, milliseconds));
    }

    private sealed record StartupWorkDiagnostics(
        IReadOnlyList<ModuleStartupWorkPerformanceInfo> WorkItems,
        IReadOnlyList<ModuleStartupWorkBarrierPerformanceInfo> Barriers);

    private sealed record ModuleProfilerStateCapture(
        long Revision,
        DateTimeOffset OriginUtc,
        long? OriginTimestamp,
        long? TerminalTimestamp,
        bool IsStarted,
        ImmutableArray<ModuleCompositionMilestonePerformanceInfo> Milestones,
        ImmutableArray<ModuleSystemPhasePerformanceInfo> SystemPhases,
        ImmutableArray<ModuleProfileState.ModuleProfileDiagnosticsCapture> ModuleProfiles,
        TypeDiscoveryStatistics TypeDiscoveryStatistics,
        ImmutableArray<TypeDiscoveryQuerySummary> TypeDiscoveryQueries,
        Func<ModuleStartupWorkSnapshot>? StartupWorkSnapshotProvider);
}

/// <summary>
/// Carries one revision-consistent profiler observation for diagnostics projection outside the profiler lock.
/// </summary>
internal sealed record ModuleProfilingDiagnosticsCapture(
    long Revision,
    ModuleCompositionPerformance Performance,
    TypeDiscoveryStatistics TypeDiscoveryStatistics,
    ImmutableArray<TypeDiscoveryQuerySummary> TypeDiscoveryQueries);
