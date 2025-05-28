using System.Diagnostics;
using System.Text;
using MoLibrary.Core.Module.Models;

namespace MoLibrary.Core.Module.Features;

/// <summary>
/// Provides performance profiling capabilities for the module system.
/// Tracks initialization times, phase durations, and module-specific metrics.
/// </summary>
public static class ModuleProfiler
{
    private static readonly Stopwatch SystemStopwatch = new();
    private static readonly Dictionary<string, Stopwatch> PhaseStopwatches = new();
    private static readonly List<string> PhaseInitializationOrder = new();
    private static readonly Dictionary<Type, ModuleProfileInfo> ModuleProfiles = new();
    private static bool _isStarted;

    /// <summary>
    /// Starts the module system profiling.
    /// </summary>
    public static void StartModuleSystem()
    {
        if (_isStarted) return;
        
        SystemStopwatch.Start();
        _isStarted = true;
    }

    /// <summary>
    /// Stops the module system profiling.
    /// </summary>
    public static void StopModuleSystem()
    {
        if (!_isStarted) return;
        
        SystemStopwatch.Stop();
        _isStarted = false;
    }

    /// <summary>
    /// Starts profiling a specific phase of the module system.
    /// </summary>
    /// <param name="phaseName">The name of the phase to profile.</param>
    public static void StartPhase(string phaseName)
    {
        if (!PhaseStopwatches.TryGetValue(phaseName, out var stopwatch))
        {
            stopwatch = new Stopwatch();
            PhaseStopwatches[phaseName] = stopwatch;
            PhaseInitializationOrder.Add(phaseName);
        }
        
        stopwatch.Start();
    }

    /// <summary>
    /// Stops profiling a specific phase of the module system.
    /// </summary>
    /// <param name="phaseName">The name of the phase to stop profiling.</param>
    /// <returns>The elapsed milliseconds for this phase.</returns>
    public static long StopPhase(string phaseName)
    {
        if (!PhaseStopwatches.TryGetValue(phaseName, out var stopwatch))
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
    public static void StartModulePhase(Type moduleType, EMoModuleConfigMethods phase)
    {
        if (!ModuleProfiles.TryGetValue(moduleType, out var profile))
        {
            profile = new ModuleProfileInfo(moduleType);
            ModuleProfiles[moduleType] = profile;
        }
        
        profile.StartPhase(phase);
    }

    /// <summary>
    /// Stops profiling a specific module's phase.
    /// </summary>
    /// <param name="moduleType">The type of the module.</param>
    /// <param name="phase">The module configuration phase.</param>
    /// <returns>The elapsed milliseconds for this module phase.</returns>
    public static long StopModulePhase(Type moduleType, EMoModuleConfigMethods phase)
    {
        if (!ModuleProfiles.TryGetValue(moduleType, out var profile))
        {
            return 0;
        }
        
        return profile.StopPhase(phase);
    }

    /// <summary>
    /// Gets the total elapsed time for the module system.
    /// </summary>
    /// <returns>The total elapsed milliseconds.</returns>
    public static long GetTotalElapsedMilliseconds()
    {
        return SystemStopwatch.ElapsedMilliseconds;
    }

    /// <summary>
    /// Gets all phase durations for the module system.
    /// </summary>
    /// <returns>A dictionary mapping phase names to their durations in milliseconds.</returns>
    public static Dictionary<string, long> GetPhaseDurations()
    {
        return PhaseStopwatches.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ElapsedMilliseconds
        );
    }

    /// <summary>
    /// Gets the duration for a specific phase.
    /// </summary>
    /// <param name="phaseName">The name of the phase.</param>
    /// <returns>The duration of the phase in milliseconds.</returns>
    public static long GetPhaseDuration(string phaseName)
    {
        return PhaseStopwatches.TryGetValue(phaseName, out var stopwatch) 
            ? stopwatch.ElapsedMilliseconds 
            : 0;
    }

    /// <summary>
    /// Gets profile information for all modules, sorted by total duration in descending order.
    /// </summary>
    /// <returns>A list of module profile information.</returns>
    public static List<ModuleProfileInfo> GetModuleProfilesSortedByTotalDuration()
    {
        return ModuleProfiles.Values
            .OrderByDescending(p => p.GetTotalDuration())
            .ToList();
    }

    /// <summary>
    /// Gets profile information for all modules, sorted by a specific phase duration in descending order.
    /// </summary>
    /// <param name="phase">The phase to sort by.</param>
    /// <returns>A list of module profile information.</returns>
    public static List<ModuleProfileInfo> GetModuleProfilesSortedByPhaseDuration(EMoModuleConfigMethods phase)
    {
        return ModuleProfiles.Values
            .OrderByDescending(p => p.GetPhaseDuration(phase))
            .ToList();
    }

    /// <summary>
    /// Gets a formatted string summary of module system performance.
    /// </summary>
    /// <returns>A string containing performance summary information.</returns>
    public static string GetPerformanceSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Module System Performance Summary:");
        sb.AppendLine($"Total initialization time: {GetTotalElapsedMilliseconds()}ms");
        
        sb.AppendLine("\nPhase Durations (in initialization order):");
        foreach (var phaseName in PhaseInitializationOrder)
        {
            if (PhaseStopwatches.TryGetValue(phaseName, out var stopwatch))
            {
                sb.AppendLine($"  {phaseName}: {stopwatch.ElapsedMilliseconds}ms");
            }
        }
        
        sb.AppendLine("\nTop 5 Slowest Modules (by total duration):");
        foreach (var profile in GetModuleProfilesSortedByTotalDuration().Take(5))
        {
            sb.AppendLine($"  {profile.ModuleType.Name}: {profile.GetTotalDuration()}ms");
            foreach (var phase in profile.GetPhaseDurations())
            {
                sb.AppendLine($"    {phase.Key}: {phase.Value}ms");
            }
        }
        
        return sb.ToString();
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
    
    private readonly Dictionary<EMoModuleConfigMethods, Stopwatch> _phaseStopwatches = new();
    
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
    public void StartPhase(EMoModuleConfigMethods phase)
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
    public long StopPhase(EMoModuleConfigMethods phase)
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
    public long GetPhaseDuration(EMoModuleConfigMethods phase)
    {
        return _phaseStopwatches.TryGetValue(phase, out var stopwatch) 
            ? stopwatch.ElapsedMilliseconds 
            : 0;
    }
    
    /// <summary>
    /// Gets the total duration across all phases for this module.
    /// </summary>
    /// <returns>The total duration in milliseconds.</returns>
    public long GetTotalDuration()
    {
        return _phaseStopwatches.Values.Sum(s => s.ElapsedMilliseconds);
    }
    
    /// <summary>
    /// Gets all phase durations for this module.
    /// </summary>
    /// <returns>A dictionary mapping phase types to their durations in milliseconds.</returns>
    public Dictionary<EMoModuleConfigMethods, long> GetPhaseDurations()
    {
        return _phaseStopwatches.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.ElapsedMilliseconds
        );
    }
} 