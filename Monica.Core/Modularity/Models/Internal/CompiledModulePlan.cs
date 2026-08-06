using Monica.Core.Modularity.Abstractions;
using Monica.Core.TypeDiscovery.Services;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Immutable executable module plan produced before Monica mutates the host builder or service collection.
/// </summary>
internal sealed class CompiledModulePlan
{
    internal CompiledModulePlan(
        CompiledModuleGraph graph,
        IReadOnlyList<ModuleRegistrationState> registrations,
        IReadOnlyList<CompiledTypeDiscoveryPlan> typeDiscoveryPlans,
        TypeDiscoveryCompilation typeDiscoveryCompilation)
    {
        Graph = graph;
        Registrations = registrations;
        TypeDiscoveryPlans = typeDiscoveryPlans;
        TypeDiscoveryCompilation = typeDiscoveryCompilation;
    }

    internal CompiledModuleGraph Graph { get; }

    internal IReadOnlyList<ModuleRegistrationState> Registrations { get; }

    internal IReadOnlyList<CompiledTypeDiscoveryPlan> TypeDiscoveryPlans { get; }

    internal TypeDiscoveryCompilation TypeDiscoveryCompilation { get; }
}

/// <summary>
/// Associates one frozen type-discovery declaration with its owning module registration.
/// </summary>
internal sealed record CompiledTypeDiscoveryPlan(
    ModuleRegistrationState Registration,
    ITypeDiscoveryPlan Plan);
