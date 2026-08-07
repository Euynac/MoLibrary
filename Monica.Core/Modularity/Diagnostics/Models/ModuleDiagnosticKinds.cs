namespace Monica.Core.Modularity.Diagnostics.Models;

/// <summary>
/// Identifies a strongly typed system-owned stage on the module-composition timeline.
/// </summary>
/// <remarks>
/// Module lifecycle callbacks are represented separately by <see cref="ModuleCallbackKind"/> and
/// <c>ModulePhase</c>. These values describe framework orchestration whose meaning must remain stable for
/// diagnostics, exports, and telemetry.
/// </remarks>
public enum ModuleSystemStage
{
    /// <summary>The application callback passed to <c>AddMonica(...)</c>.</summary>
    ApplicationConfiguration = 0,

    /// <summary>Modules declare their structural type-discovery queries.</summary>
    TypeDiscoveryPlanDeclaration,

    /// <summary>The configured discovery assembly set is resolved.</summary>
    TypeDiscoveryAssemblyResolution,

    /// <summary>Business types are enumerated from the resolved assemblies.</summary>
    TypeDiscoveryTypeEnumeration,

    /// <summary>Distinct structural queries are evaluated against the business-type snapshot.</summary>
    TypeDiscoveryQueryEvaluation,

    /// <summary>Compiled query matches are committed to their owning modules.</summary>
    TypeDiscoveryRegistrationCommit
}

/// <summary>
/// Classifies a profiled module callback by its runtime responsibility.
/// </summary>
public enum ModuleCallbackKind
{
    /// <summary>A callback implemented directly by a module lifecycle method.</summary>
    Lifecycle = 0,

    /// <summary>A callback contributed by a host registration extension.</summary>
    RegistrationContribution,

    /// <summary>A callback that commits previously compiled type-discovery matches.</summary>
    TypeDiscoveryCommit,

    /// <summary>A serial callback that commits a completed startup-work result.</summary>
    StartupWorkCommit
}
