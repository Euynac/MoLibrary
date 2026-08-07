using Monica.Core.Modularity.Abstractions;
using Monica.Core.TypeDiscovery.Services;

namespace Monica.Core.Modularity.Models.Internal;

/// <summary>
/// Holds the short-lived output of one type-discovery compilation until its serial commits complete.
/// </summary>
/// <remarks>
/// The module graph is owned separately by <see cref="CompiledModuleGraph"/>. Releasing this object drops every
/// compiler-owned reflection shape and commit callback so discovery metadata cannot survive for the host lifetime.
/// </remarks>
internal sealed class CompiledTypeDiscovery
{
    private IReadOnlyList<CompiledTypeDiscoveryPlan> _plans;
    private TypeDiscoveryCompilation? _compilation;

    internal CompiledTypeDiscovery(
        IReadOnlyList<CompiledTypeDiscoveryPlan> plans,
        TypeDiscoveryCompilation compilation)
    {
        _plans = plans;
        _compilation = compilation;
    }

    internal IReadOnlyList<CompiledTypeDiscoveryPlan> Plans => _plans;

    internal TypeDiscoveryCompilation Compilation => _compilation
        ?? throw new InvalidOperationException("The compiled type-discovery data has already been released.");

    internal void Release()
    {
        foreach (var plan in _plans)
        {
            plan.Plan.Release();
        }

        _plans = [];
        _compilation?.Release();
        _compilation = null;
    }
}

/// <summary>
/// Associates one non-empty frozen type-discovery declaration with its owning module registration.
/// </summary>
internal sealed record CompiledTypeDiscoveryPlan(
    ModuleRegistrationState Registration,
    ITypeDiscoveryPlan Plan);
