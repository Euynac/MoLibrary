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
    private readonly ModuleProfilingState _state = new();

    /// <summary>
    /// Gets whether the full module-composition stopwatch is currently running.
    /// </summary>
    internal bool IsRunning => _state.IsStarted;

    /// <summary>
    /// Clears all profiling data for this host.
    /// </summary>
    internal void Clear()
    {
        _state.Clear();
    }

    /// <summary>
    /// Starts the module composition timeline at the <c>AddMonica(...)</c> boundary.
    /// </summary>
    internal void StartModuleSystem()
    {
        if (_state.IsStarted)
        {
            return;
        }

        _state.OriginUtc = DateTimeOffset.UtcNow;
        _state.OriginTimestamp = Stopwatch.GetTimestamp();
        _state.TerminalTimestamp = null;
        _state.IsStarted = true;
        RecordMilestone(ModuleCompositionMilestone.CompositionStarted);
    }

    /// <summary>
    /// Stops the end-to-end composition timer without inventing a successful completion milestone.
    /// </summary>
    internal void StopModuleSystem()
    {
        if (!_state.IsStarted)
        {
            return;
        }

        _state.TerminalTimestamp = Stopwatch.GetTimestamp();
        _state.IsStarted = false;
    }

    /// <summary>
    /// Records one unique host-owned lifecycle milestone.
    /// </summary>
    /// <param name="milestone">The milestone that just occurred.</param>
    internal void RecordMilestone(ModuleCompositionMilestone milestone)
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
    }

    /// <summary>
    /// Starts one system-level serial phase execution.
    /// </summary>
    /// <param name="phaseName">The profiler phase name.</param>
    internal void StartPhase(string phaseName)
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
                DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Completes one system-level serial phase execution.
    /// </summary>
    /// <param name="phaseName">The profiler phase name.</param>
    /// <returns>The completed phase duration in whole milliseconds.</returns>
    internal long StopPhase(string phaseName)
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
            StartedAtUtc = start.StartedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            StartedOffsetMs = GetOffsetMs(start.StartedTimestamp),
            CompletedOffsetMs = GetOffsetMs(completedTimestamp)
        };
        _state.SystemPhases.Add(execution);
        return ToWholeMilliseconds(execution.DurationMs);
    }

    /// <summary>
    /// Starts one serial callback execution for a module phase.
    /// </summary>
    internal void StartModulePhase(
        Type moduleType,
        ModuleKey moduleKey,
        int registrationOrder,
        ModulePhase phase)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        GetOrCreateModuleProfile(moduleType, moduleKey, registrationOrder).StartPhase(
            phase,
            NextSequence(),
            Stopwatch.GetTimestamp(),
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Completes one serial callback execution for a module phase.
    /// </summary>
    internal long StopModulePhase(Type moduleType, ModulePhase phase)
    {
        return _state.ModuleProfiles.TryGetValue(moduleType, out var profile)
            ? ToWholeMilliseconds(profile.StopPhase(phase, Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow))
            : 0;
    }

    /// <summary>
    /// Merges one immutable scheduler snapshot into the host composition timeline.
    /// </summary>
    internal void RecordCompositionWork(ModuleCompositionWorkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var checkpoints = snapshot.Checkpoints.Select(checkpoint =>
            new ModuleCompositionCheckpointPerformanceInfo
            {
                Sequence = checkpoint.Sequence,
                Deadline = checkpoint.Deadline,
                EnteredAtUtc = checkpoint.EnteredAtUtc,
                ReleasedAtUtc = checkpoint.ReleasedAtUtc,
                EnteredOffsetMs = GetOffsetMs(checkpoint.EnteredTimestamp),
                ReleasedOffsetMs = GetOffsetMs(checkpoint.ReleasedTimestamp),
                DueWorkItemIds = checkpoint.WorkItems.Select(static work => work.WorkItemId).ToArray(),
                PendingWorkItems = checkpoint.PendingWorkItems.Select(static pending =>
                    new ModuleCompositionCheckpointPendingWorkInfo
                    {
                        WorkItemId = pending.WorkItemId,
                        RemainingDurationMs = pending.RemainingDuration.TotalMilliseconds
                    }).ToArray(),
                ReleasingWorkItemId = checkpoint.ReleasingWorkItemId
            }).ToArray();
        var checkpointByWorkItemId = checkpoints
            .SelectMany(checkpoint => checkpoint.DueWorkItemIds.Select(workItemId => (workItemId, checkpoint)))
            .ToDictionary(static pair => pair.workItemId, static pair => pair.checkpoint, StringComparer.Ordinal);

        foreach (var result in snapshot.WorkItems)
        {
            checkpointByWorkItemId.TryGetValue(result.WorkItemId, out var checkpoint);
            var pending = checkpoint?.PendingWorkItems.FirstOrDefault(item =>
                string.Equals(item.WorkItemId, result.WorkItemId, StringComparison.Ordinal));
            _state.CompositionWorkItems.Add(new ModuleCompositionWorkPerformanceInfo
            {
                WorkItemId = result.WorkItemId,
                Sequence = result.Sequence,
                ModuleKey = result.ModuleKey,
                ModuleTypeName = result.ModuleType.Name,
                ModuleFullTypeName = result.ModuleType.FullName ?? result.ModuleType.Name,
                ModuleRegistrationOrder = result.RegistrationOrder,
                Name = result.Name,
                OriginPhase = result.OriginPhase,
                Deadline = result.Deadline,
                Status = result.IsSucceeded
                    ? ModuleCompositionWorkStatus.Succeeded
                    : ModuleCompositionWorkStatus.Failed,
                SubmittedAtUtc = result.SubmittedAtUtc,
                StartedAtUtc = result.StartedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                SubmittedOffsetMs = GetOffsetMs(result.SubmittedTimestamp),
                StartedOffsetMs = GetOffsetMs(result.StartedTimestamp),
                CompletedOffsetMs = GetOffsetMs(result.CompletedTimestamp),
                WasPendingAtDeadline = pending is not null,
                RemainingAtDeadlineMs = pending?.RemainingDurationMs ?? 0,
                IsDeadlineReleaser = string.Equals(
                    checkpoint?.ReleasingWorkItemId,
                    result.WorkItemId,
                    StringComparison.Ordinal),
                ErrorMessage = result.Failure?.GetMessageRecursively()
            });
        }

        _state.CompositionCheckpoints.AddRange(checkpoints);
    }

    /// <summary>
    /// Builds the immutable end-to-end composition performance model.
    /// </summary>
    internal ModuleCompositionPerformance GetCompositionPerformance()
    {
        var moduleExecutions = _state.ModuleProfiles.Values
            .SelectMany(profile => profile.CreateExecutions(GetOffsetMs))
            .OrderBy(static execution => execution.Sequence)
            .ToArray();

        return new ModuleCompositionPerformance
        {
            StartedAtUtc = _state.OriginUtc,
            ElapsedDurationMs = GetElapsedDurationMs(),
            Milestones = _state.Milestones.OrderBy(static milestone => milestone.Sequence).ToArray(),
            SystemPhases = _state.SystemPhases.OrderBy(static phase => phase.Sequence).ToArray(),
            ModulePhaseExecutions = moduleExecutions,
            WorkItems = _state.CompositionWorkItems.OrderBy(static work => work.Sequence).ToArray(),
            Checkpoints = _state.CompositionCheckpoints.OrderBy(static checkpoint => checkpoint.Sequence).ToArray()
        };
    }

    /// <summary>
    /// Creates the module-oriented performance projection used by details and tables.
    /// </summary>
    internal ModulePerformanceInfo GetModulePerformance(ModuleRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var phaseExecutions = _state.ModuleProfiles.TryGetValue(snapshot.ModuleType, out var profile)
            ? profile.CreateExecutions(GetOffsetMs)
            : [];
        return new ModulePerformanceInfo
        {
            ModuleKey = snapshot.ModuleKey,
            ModuleTypeName = snapshot.ModuleType.Name,
            ModuleFullTypeName = snapshot.ModuleType.FullName ?? snapshot.ModuleType.Name,
            RegistrationOrder = snapshot.RegisterInfo.Order,
            IsRuntimeAvailable = true,
            PhaseExecutions = phaseExecutions,
            CompositionWorkItems = _state.CompositionWorkItems
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
        return _state.ModuleProfiles.Values
            .OrderBy(static profile => profile.RegistrationOrder)
            .ThenBy(static profile => profile.ModuleType.FullName, StringComparer.Ordinal)
            .Select(profile => new ModulePerformanceInfo
            {
                ModuleKey = profile.ModuleKey,
                ModuleTypeName = profile.ModuleType.Name,
                ModuleFullTypeName = profile.ModuleType.FullName ?? profile.ModuleType.Name,
                RegistrationOrder = profile.RegistrationOrder,
                IsRuntimeAvailable = runtimeModuleKeys.Contains(profile.ModuleKey),
                PhaseExecutions = profile.CreateExecutions(GetOffsetMs),
                CompositionWorkItems = _state.CompositionWorkItems
                    .Where(work => work.ModuleKey == profile.ModuleKey)
                    .OrderBy(static work => work.Sequence)
                    .ToArray()
            })
            .ToArray();
    }

    /// <summary>
    /// Gets module profiles sorted by aggregate serial callback duration.
    /// </summary>
    internal List<ModuleProfileState> GetModuleProfilesSortedBySerialPhaseDuration()
    {
        return _state.ModuleProfiles.Values
            .OrderByDescending(static profile => profile.GetSerialPhaseDurationMs())
            .ToList();
    }

    /// <summary>
    /// Gets a concise factual summary without treating overlapping timing dimensions as additive.
    /// </summary>
    internal string GetPerformanceSummary()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Module System Performance Summary:");
        builder.AppendLine($"End-to-end composition elapsed: {GetElapsedDurationMs():F1}ms");
        builder.AppendLine($"Aggregate system phases: {_state.SystemPhases.Sum(static phase => phase.DurationMs):F1}ms");
        builder.AppendLine(
            $"Aggregate serial module callbacks: {_state.ModuleProfiles.Values.Sum(static profile => profile.GetSerialPhaseDurationMs()):F1}ms");

        if (_state.CompositionWorkItems.Count > 0)
        {
            var activeSpan = _state.CompositionWorkItems.Max(static work => work.CompletedOffsetMs)
                             - _state.CompositionWorkItems.Min(static work => work.StartedOffsetMs);
            builder.AppendLine($"Parallel work active span: {activeSpan:F1}ms");
            builder.AppendLine(
                $"Aggregate worker execution: {_state.CompositionWorkItems.Sum(static work => work.ExecutionDurationMs):F1}ms");
            builder.AppendLine(
                $"Aggregate checkpoint wait: {_state.CompositionCheckpoints.Sum(static checkpoint => checkpoint.BlockingWaitDurationMs):F1}ms");
        }

        builder.AppendLine("Slowest serial module callbacks:");
        foreach (var profile in GetModuleProfilesSortedBySerialPhaseDuration().Take(5))
        {
            builder.AppendLine($"  {profile.ModuleType.Name}: {profile.GetSerialPhaseDurationMs():F1}ms");
        }

        return builder.ToString();
    }

    /// <summary>
    /// Gets aggregate serial callback time for one module in whole milliseconds.
    /// </summary>
    internal long GetModuleSerialPhaseDuration(Type moduleType)
    {
        return _state.ModuleProfiles.TryGetValue(moduleType, out var profile)
            ? ToWholeMilliseconds(profile.GetSerialPhaseDurationMs())
            : 0;
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
        if (_state.OriginTimestamp is not { } originTimestamp || timestamp <= originTimestamp)
        {
            return 0;
        }

        return Stopwatch.GetElapsedTime(originTimestamp, timestamp).TotalMilliseconds;
    }

    private double GetElapsedDurationMs()
    {
        if (_state.OriginTimestamp is not { } originTimestamp)
        {
            return 0;
        }

        var terminalTimestamp = _state.TerminalTimestamp ?? Stopwatch.GetTimestamp();
        return Stopwatch.GetElapsedTime(originTimestamp, terminalTimestamp).TotalMilliseconds;
    }

    private static string FormatExecutionId(string prefix, long sequence)
    {
        return $"{prefix}-{sequence:D6}";
    }

    private static long ToWholeMilliseconds(double milliseconds)
    {
        return (long)Math.Floor(Math.Max(0, milliseconds));
    }

}
