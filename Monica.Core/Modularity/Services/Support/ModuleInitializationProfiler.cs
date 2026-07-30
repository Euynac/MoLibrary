using System.Diagnostics;
using System.Text;
using Monica.Core.Extensions;
using Monica.Core.Modularity.Diagnostics.Models;
using Monica.Core.Modularity.State;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Services.Support;

/// <summary>
/// Provides performance profiling capabilities for the module system.
/// Tracks initialization times, phase durations, and module-specific metrics.
/// </summary>
internal sealed class ModuleInitializationProfiler
{
    private readonly ModuleProfilingState _state = new();

    /// <summary>
    /// Gets whether the full module-system composition stopwatch is currently running.
    /// </summary>
    internal bool IsRunning => _state.IsStarted;

    /// <summary>
    /// Clears all profiling data for this host.
    /// </summary>
    public void Clear()
    {
        _state.Clear();
    }

    /// <summary>
    /// Starts the module system profiling.
    /// </summary>
    public void StartModuleSystem()
    {
        var state = _state;
        if (state.IsStarted) return;
        
        state.SystemStopwatch.Start();
        state.IsStarted = true;
    }

    /// <summary>
    /// Stops the module system profiling.
    /// </summary>
    public void StopModuleSystem()
    {
        var state = _state;
        if (!state.IsStarted) return;
        
        state.SystemStopwatch.Stop();
        state.IsStarted = false;
    }

    /// <summary>
    /// Starts profiling a specific phase of the module system.
    /// </summary>
    /// <param name="phaseName">The name of the phase to profile.</param>
    public void StartPhase(string phaseName)
    {
        var state = _state;
        if (!state.PhaseStopwatches.TryGetValue(phaseName, out var stopwatch))
        {
            stopwatch = new Stopwatch();
            state.PhaseStopwatches[phaseName] = stopwatch;
            state.PhaseInitializationOrder.Add(phaseName);
        }
        
        stopwatch.Start();
    }

    /// <summary>
    /// Stops profiling a specific phase of the module system.
    /// </summary>
    /// <param name="phaseName">The name of the phase to stop profiling.</param>
    /// <returns>The elapsed milliseconds for this phase.</returns>
    public long StopPhase(string phaseName)
    {
        if (!_state.PhaseStopwatches.TryGetValue(phaseName, out var stopwatch))
        {
            return 0;
        }
        
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Starts profiling a specific module's phase.
    /// </summary>
    /// <param name="moduleType">The type of the module.</param>
    /// <param name="phase">The module configuration phase.</param>
    public void StartModulePhase(Type moduleType, ModulePhase phase)
    {
        var state = _state;
        if (!state.ModuleProfiles.TryGetValue(moduleType, out var profile))
        {
            profile = new ModuleProfileInfo(moduleType);
            state.ModuleProfiles[moduleType] = profile;
        }
        
        profile.StartPhase(phase);
    }

    /// <summary>
    /// Stops profiling a specific module's phase.
    /// </summary>
    /// <param name="moduleType">The type of the module.</param>
    /// <param name="phase">The module configuration phase.</param>
    /// <returns>The elapsed milliseconds for this module phase.</returns>
    public long StopModulePhase(Type moduleType, ModulePhase phase)
    {
        if (!_state.ModuleProfiles.TryGetValue(moduleType, out var profile))
        {
            return 0;
        }
        
        return profile.StopPhase(phase);
    }

    /// <summary>
    /// Gets the total elapsed time for the module system.
    /// </summary>
    /// <returns>The total elapsed milliseconds.</returns>
    public long GetTotalElapsedMilliseconds()
    {
        return _state.SystemStopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Gets all phase durations for the module system.
    /// </summary>
    /// <returns>A dictionary mapping phase names to their durations in milliseconds.</returns>
    public Dictionary<string, long> GetPhaseDurations()
    {
        return _state.PhaseStopwatches.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// Gets the duration for a specific phase.
    /// </summary>
    /// <param name="phaseName">The name of the phase.</param>
    /// <returns>The duration of the phase in milliseconds.</returns>
    public long GetPhaseDuration(string phaseName)
    {
        return _state.PhaseStopwatches.TryGetValue(phaseName, out var stopwatch)
            ? stopwatch.ElapsedMilliseconds 
            : 0;
    }

    /// <summary>
    /// Merges one complete immutable scheduler snapshot into diagnostics on the serial composition thread.
    /// </summary>
    internal void RecordCompositionWork(ModuleCompositionWorkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.WorkItems.Count > 0)
        {
            var startedTimestamp = snapshot.WorkItems.Min(static result => result.StartedTimestamp);
            var completedTimestamp = snapshot.WorkItems.Max(static result => result.CompletedTimestamp);
            _state.CompositionWorkWallDurationMs = (long)Stopwatch
                .GetElapsedTime(startedTimestamp, completedTimestamp)
                .TotalMilliseconds;
        }

        foreach (var result in snapshot.WorkItems)
        {
            if (!_state.ModuleProfiles.TryGetValue(result.ModuleType, out var profile))
            {
                profile = new ModuleProfileInfo(result.ModuleType);
                _state.ModuleProfiles.Add(result.ModuleType, profile);
            }

            profile.AddCompositionWork(new ModuleCompositionWorkPerformanceInfo
            {
                Name = result.Name,
                OriginPhase = result.OriginPhase,
                Deadline = result.Deadline,
                Status = result.IsSucceeded
                    ? ModuleCompositionWorkStatus.Succeeded
                    : ModuleCompositionWorkStatus.Failed,
                SubmittedAtUtc = result.SubmittedAtUtc,
                StartedAtUtc = result.StartedAtUtc,
                CompletedAtUtc = result.CompletedAtUtc,
                QueueDurationMs = (long)result.QueueDuration.TotalMilliseconds,
                ExecutionDurationMs = (long)result.ExecutionDuration.TotalMilliseconds,
                ErrorMessage = result.Failure?.GetMessageRecursively()
            });
        }

        _state.CompositionCheckpoints.AddRange(snapshot.Checkpoints.Select(static checkpoint =>
            new ModuleCompositionCheckpointPerformanceInfo
            {
                Deadline = checkpoint.Deadline,
                WorkItemCount = checkpoint.WorkItemCount,
                WaitDurationMs = (long)checkpoint.WaitDuration.TotalMilliseconds
            }));
    }

    /// <summary>
    /// Gets wall-clock, aggregate worker, queue, and checkpoint wait time for scheduled composition work.
    /// </summary>
    internal ModuleCompositionWorkSummary GetCompositionWorkSummary()
    {
        var workItems = _state.ModuleProfiles.Values
            .SelectMany(static profile => profile.GetCompositionWork())
            .ToArray();
        if (workItems.Length == 0)
        {
            return ModuleCompositionWorkSummary.Empty with
            {
                Checkpoints = _state.CompositionCheckpoints.ToArray(),
                TotalCheckpointWaitDurationMs = _state.CompositionCheckpoints.Sum(static checkpoint => checkpoint.WaitDurationMs)
            };
        }

        return new ModuleCompositionWorkSummary(
            workItems.Length,
            _state.CompositionWorkWallDurationMs,
            workItems.Sum(static work => work.ExecutionDurationMs),
            workItems.Sum(static work => work.QueueDurationMs),
            _state.CompositionCheckpoints.Sum(static checkpoint => checkpoint.WaitDurationMs),
            _state.CompositionCheckpoints.ToArray());
    }

    /// <summary>
    /// Gets profile information for all modules, sorted by aggregate serial phase duration in descending order.
    /// </summary>
    /// <returns>A list of module profile information.</returns>
    public List<ModuleProfileInfo> GetModuleProfilesSortedBySerialPhaseDuration()
    {
        return _state.ModuleProfiles.Values
            .OrderByDescending(static profile => profile.GetSerialPhaseDuration())
            .ToList();
    }

    /// <summary>
    /// Gets profile information for all modules, sorted by a specific phase duration in descending order.
    /// </summary>
    /// <param name="phase">The phase to sort by.</param>
    /// <returns>A list of module profile information.</returns>
    public List<ModuleProfileInfo> GetModuleProfilesSortedByPhaseDuration(ModulePhase phase)
    {
        return _state.ModuleProfiles.Values
            .OrderByDescending(p => p.GetPhaseDuration(phase))
            .ToList();
    }

    /// <summary>
    /// Gets a formatted string summary of module system performance.
    /// </summary>
    /// <returns>A string containing performance summary information.</returns>
    public string GetPerformanceSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Module System Performance Summary:");
        sb.AppendLine($"Total initialization time: {GetTotalElapsedMilliseconds()}ms");
        
        sb.AppendLine("\nPhase Durations (in initialization order):");
        var totalSystemPhaseDuration = 0L;
        var systemPhaseCount = 0;
        var state = _state;
        
        foreach (var phaseName in state.PhaseInitializationOrder)
        {
            if (state.PhaseStopwatches.TryGetValue(phaseName, out var stopwatch))
            {
                sb.AppendLine($"  {phaseName}: {stopwatch.ElapsedMilliseconds}ms");
                totalSystemPhaseDuration += stopwatch.ElapsedMilliseconds;
                systemPhaseCount++;
            }
        }
        
        if (systemPhaseCount > 0)
        {
            sb.AppendLine($"\nAll System Phases Total Duration: {totalSystemPhaseDuration}ms (across {systemPhaseCount} phases)");
        }
        
        // Add module phase statistics
        sb.AppendLine("\nModule Phase Statistics:");
        var allPhases = Enum.GetValues<ModulePhase>();
        
        // Calculate total duration across all module phases
        var totalModulePhaseDuration = 0L;
        var totalModulePhaseCount = 0;
        
        foreach (var phase in allPhases)
        {
            var moduleCount = 0;
            var totalDuration = 0L;
            var maxDuration = 0L;
            string? slowestModule = null;
            
            foreach (var profile in state.ModuleProfiles.Values)
            {
                var duration = profile.GetPhaseDuration(phase);
                if (duration > 0)
                {
                    moduleCount++;
                    totalDuration += duration;
                    totalModulePhaseDuration += duration;
                    if (duration > maxDuration)
                    {
                        maxDuration = duration;
                        slowestModule = profile.ModuleType.Name;
                    }
                }
            }
            
            if (moduleCount > 0)
            {
                totalModulePhaseCount += moduleCount;
                var averageDuration = totalDuration / moduleCount;
                sb.AppendLine($"  {phase}:");
                sb.AppendLine($"    Total: {totalDuration}ms | Average: {averageDuration}ms | Modules: {moduleCount}");
                sb.AppendLine($"    Slowest: {slowestModule} ({maxDuration}ms)");
            }
        }
        
        // Add total summary at the end
        if (totalModulePhaseCount > 0)
        {
            sb.AppendLine($"\nAll Module Phases Total Duration: {totalModulePhaseDuration}ms (across {totalModulePhaseCount} phase executions)");
        }

        var compositionWork = GetCompositionWorkSummary();
        if (compositionWork.Count > 0)
        {
            sb.AppendLine("\nComposition Work:");
            sb.AppendLine($"  Wall time: {compositionWork.WallDurationMs}ms");
            sb.AppendLine($"  Aggregate worker time: {compositionWork.TotalExecutionDurationMs}ms across {compositionWork.Count} items");
            sb.AppendLine($"  Aggregate checkpoint wait: {compositionWork.TotalCheckpointWaitDurationMs}ms");
        }
        
        sb.AppendLine("\nTop 5 Slowest Modules (by serial callback duration):");
        foreach (var profile in GetModuleProfilesSortedBySerialPhaseDuration().Take(5))
        {
            sb.AppendLine($"  {profile.ModuleType.Name}: {profile.GetSerialPhaseDuration()}ms");
            foreach (var phase in profile.GetPhaseDurations())
            {
                sb.AppendLine($"    {phase.Key}: {phase.Value}ms");
            }

            foreach (var workItem in profile.GetCompositionWork())
            {
                sb.AppendLine(
                    $"    CompositionWork[{workItem.Name}]: {workItem.ExecutionDurationMs}ms ({workItem.Status}, {workItem.Deadline})");
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Gets profile information for a specific module type.
    /// </summary>
    /// <param name="moduleType">The type of the module to get profile information for.</param>
    /// <returns>The ModuleProfileInfo for the specified module type, or null if not found.</returns>
    public ModuleProfileInfo? GetModuleProfile(Type moduleType)
    {
        return _state.ModuleProfiles.GetValueOrDefault(moduleType);
    }

    /// <summary>
    /// Gets the aggregate serial phase duration for a specific module type.
    /// </summary>
    /// <param name="moduleType">The type of the module to get duration for.</param>
    /// <returns>The serial phase duration in milliseconds, or 0 if not found.</returns>
    public long GetModuleSerialPhaseDuration(Type moduleType)
    {
        return _state.ModuleProfiles.TryGetValue(moduleType, out var profile)
            ? profile.GetSerialPhaseDuration()
            : 0;
    }
}

/// <summary>
/// Contains performance profiling information for a specific module.
/// </summary>
public class ModuleProfileInfo
{
    /// <summary>
    /// The type of the module being profiled.
    /// </summary>
    public Type ModuleType { get; }
    
    private readonly Dictionary<ModulePhase, Stopwatch> _phaseStopwatches = new();
    private readonly List<ModuleCompositionWorkPerformanceInfo> _compositionWork = [];
    
    /// <summary>
    /// Creates a new module profile information instance.
    /// </summary>
    /// <param name="moduleType">The type of the module.</param>
    public ModuleProfileInfo(Type moduleType)
    {
        ModuleType = moduleType;
    }
    
    /// <summary>
    /// Starts profiling a specific phase for this module.
    /// </summary>
    /// <param name="phase">The module configuration phase.</param>
    public void StartPhase(ModulePhase phase)
    {
        if (!_phaseStopwatches.TryGetValue(phase, out var stopwatch))
        {
            stopwatch = new Stopwatch();
            _phaseStopwatches[phase] = stopwatch;
        }
        
        stopwatch.Start();
    }
    
    /// <summary>
    /// Stops profiling a specific phase for this module.
    /// </summary>
    /// <param name="phase">The module configuration phase.</param>
    /// <returns>The elapsed milliseconds for this phase.</returns>
    public long StopPhase(ModulePhase phase)
    {
        if (!_phaseStopwatches.TryGetValue(phase, out var stopwatch))
        {
            return 0;
        }
        
        stopwatch.Stop();
        return stopwatch.ElapsedMilliseconds;
    }
    
    /// <summary>
    /// Gets the duration for a specific phase.
    /// </summary>
    /// <param name="phase">The module configuration phase.</param>
    /// <returns>The duration in milliseconds.</returns>
    public long GetPhaseDuration(ModulePhase phase)
    {
        return _phaseStopwatches.TryGetValue(phase, out var stopwatch) 
            ? stopwatch.ElapsedMilliseconds 
            : 0;
    }
    
    /// <summary>
    /// Gets the aggregate duration across this module's serial composition phases.
    /// </summary>
    /// <returns>The serial phase duration in milliseconds.</returns>
    public long GetSerialPhaseDuration()
    {
        return _phaseStopwatches.Values.Sum(static stopwatch => stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Gets composition work in stable submission order.
    /// </summary>
    public IReadOnlyList<ModuleCompositionWorkPerformanceInfo> GetCompositionWork()
    {
        return _compositionWork;
    }

    /// <summary>
    /// Records one composition work item for the module.
    /// </summary>
    internal void AddCompositionWork(ModuleCompositionWorkPerformanceInfo workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        _compositionWork.Add(workItem);
    }
    
    /// <summary>
    /// Gets all phase durations for this module.
    /// </summary>
    /// <returns>A dictionary mapping phase types to their durations in milliseconds.</returns>
    public Dictionary<ModulePhase, long> GetPhaseDurations()
    {
        return _phaseStopwatches.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ElapsedMilliseconds
        );
    }
}

/// <summary>
/// Aggregates required composition work without conflating parallel work, queueing, and checkpoint waits.
/// </summary>
internal sealed record ModuleCompositionWorkSummary(
    int Count,
    long WallDurationMs,
    long TotalExecutionDurationMs,
    long TotalQueueDurationMs,
    long TotalCheckpointWaitDurationMs,
    IReadOnlyList<ModuleCompositionCheckpointPerformanceInfo> Checkpoints)
{
    internal static ModuleCompositionWorkSummary Empty { get; } = new(0, 0, 0, 0, 0, []);
}
