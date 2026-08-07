using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Groups serial callback executions and scheduled startup work owned by one module.
/// </summary>
internal sealed class ModulePerformanceInfo
{
    /// <summary>Gets the stable module key.</summary>
    public ModuleKey ModuleKey { get; init; }

    /// <summary>Gets the short CLR type name.</summary>
    public string ModuleTypeName { get; init; } = string.Empty;

    /// <summary>Gets the fully qualified CLR type name.</summary>
    public string ModuleFullTypeName { get; init; } = string.Empty;

    /// <summary>Gets the dependency-aware module registration order.</summary>
    public int RegistrationOrder { get; init; }

    /// <summary>Gets whether the module has a runtime snapshot that can be opened in module details.</summary>
    public bool IsRuntimeAvailable { get; init; }

    /// <summary>Gets every serial callback execution, including repeated executions of the same phase.</summary>
    public IReadOnlyList<ModulePhaseExecutionPerformanceInfo> PhaseExecutions { get; init; } = [];

    /// <summary>Gets startup work scheduled by this module in stable submission order.</summary>
    public IReadOnlyList<ModuleStartupWorkPerformanceInfo> StartupWorkItems { get; init; } = [];

    /// <summary>Gets the aggregate duration of this module's serial callbacks.</summary>
    public double SerialDurationMs => PhaseExecutions.Sum(static execution => execution.DurationMs);

    /// <summary>Gets the aggregate duration of the supplied phase across all of its executions.</summary>
    /// <param name="phase">The phase to aggregate.</param>
    /// <returns>The aggregate monotonic duration in milliseconds.</returns>
    public double GetPhaseDurationMs(ModulePhase phase)
    {
        return PhaseExecutions
            .Where(execution => execution.Phase == phase)
            .Sum(static execution => execution.DurationMs);
    }
}
