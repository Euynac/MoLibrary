using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Models;

/// <summary>
/// Provides an immutable public view of one successfully composed module.
/// </summary>
public sealed class ModuleRuntimeSnapshot
{
    private readonly MonicaApplication _application;

    internal ModuleRuntimeSnapshot(
        MonicaApplication application,
        MonicaModule moduleInstance,
        ModuleRegistrationState registration)
    {
        _application = application;
        ModuleInstance = moduleInstance;
        RegisterInfo = registration;
        ModuleType = moduleInstance.GetType();
    }

    /// <summary>
    /// Gets the host-owned module strategy instance.
    /// </summary>
    public MonicaModule ModuleInstance { get; }

    /// <summary>
    /// Gets the concrete module strategy type.
    /// </summary>
    public Type ModuleType { get; }

    /// <summary>
    /// Gets the dependency-first execution order assigned by the graph compiler.
    /// </summary>
    public int Order => RegisterInfo.Order;

    /// <summary>
    /// Gets the latest completed or active module lifecycle phase.
    /// </summary>
    public ModulePhase Phase => RegisterInfo.ModulePhase;

    /// <summary>
    /// Gets whether the module contributes to the ASP.NET Core lifecycle.
    /// </summary>
    public bool IsWebModule => RegisterInfo.ModuleSingleton.IsWebModule;

    /// <summary>
    /// Gets whether the module requires an ASP.NET Core host adapter.
    /// </summary>
    public bool RequiresWebHost => RegisterInfo.RequiresWebHost;

    /// <summary>
    /// Gets the intrinsic or feature-selected reason this module requires an ASP.NET Core host adapter.
    /// </summary>
    public string? WebHostRequirementReason => RegisterInfo.WebHostRequirementReason;

    /// <summary>
    /// Gets the diagnostic key projected from <see cref="ModuleType"/>.
    /// </summary>
    public ModuleKey ModuleKey => _application.Dependencies.ResolveModuleKey(ModuleType);

    /// <summary>
    /// Gets the aggregate serial composition duration for this module, in milliseconds.
    /// </summary>
    public long SerialPhaseDurationMs =>
        _application.Profiling.GetModuleSerialPhaseDuration(ModuleType);

    internal ModuleRegistrationState RegisterInfo { get; }

    /// <inheritdoc />
    public override string ToString() => $"[{ModuleKey}] {RegisterInfo}";
}
