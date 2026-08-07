using Monica.Core.TypeDiscovery.Models;
using Monica.Core.TypeDiscovery.Services;
using Monica.Core.Modularity.Models.Internal;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Collects structural business-type queries and deterministic commit callbacks for one module.
/// </summary>
/// <typeparam name="TOptions">The current module's frozen option type.</typeparam>
/// <remarks>
/// A module declares all queries before scanning begins. Monica freezes every plan, performs one analysis-only scan,
/// and then invokes commits serially in module topology and declaration order. A commit may register services; query
/// evaluation never mutates the service collection.
/// </remarks>
public sealed class TypeDiscoveryPlan<TOptions> : ITypeDiscoveryPlan
    where TOptions : class, IModuleOptions, new()
{
    private readonly List<TypeDiscoveryRegistration> _registrations = [];
    private TypeDiscoveryPlanState _state;

    internal TypeDiscoveryPlan()
    {
    }

    /// <summary>
    /// Declares a structural query and the callback that will consume its immutable match set after scanning.
    /// </summary>
    /// <param name="query">The immutable structural query to evaluate.</param>
    /// <param name="commit">
    /// The deterministic commit callback. It receives this plan's context and matches in configured type-finder order.
    /// </param>
    /// <exception cref="InvalidOperationException">The plan has already been frozen for compilation.</exception>
    public void Match(
        TypeQuery query,
        Action<TypeDiscoveryContext<TOptions>, IReadOnlyList<BusinessTypeMatch>> commit)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(commit);
        EnsureCollecting();

        _registrations.Add(new TypeDiscoveryRegistration(
            query,
            (context, matches) => commit(new TypeDiscoveryContext<TOptions>(context), matches)));
    }

    IReadOnlyList<TypeDiscoveryRegistration> ITypeDiscoveryPlan.Registrations => _registrations;

    void ITypeDiscoveryPlan.Freeze()
    {
        if (_state is TypeDiscoveryPlanState.Committed or TypeDiscoveryPlanState.Released)
        {
            throw new InvalidOperationException("A completed type-discovery plan cannot be compiled again.");
        }

        _state = TypeDiscoveryPlanState.Frozen;
    }

    void ITypeDiscoveryPlan.Commit(
        TypeDiscoveryCompilation compilation,
        ModuleConfigurationContext context,
        Action callbackStarting)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(callbackStarting);
        if (_state != TypeDiscoveryPlanState.Frozen)
        {
            throw new InvalidOperationException("A type-discovery plan must be frozen and compiled before it is committed.");
        }

        // A composition failure is terminal after service mutation begins; mark first so accidental retries cannot
        // duplicate registrations after a callback throws partway through the plan.
        _state = TypeDiscoveryPlanState.Committed;
        foreach (var registration in _registrations)
        {
            callbackStarting();
            registration.Commit(context, compilation.GetMatches(registration.Query));
        }
    }

    void ITypeDiscoveryPlan.Release()
    {
        _registrations.Clear();
        _state = TypeDiscoveryPlanState.Released;
    }

    private void EnsureCollecting()
    {
        if (_state != TypeDiscoveryPlanState.Collecting)
        {
            throw new InvalidOperationException("Type queries cannot be added after the discovery plan has been frozen.");
        }
    }
}

internal interface ITypeDiscoveryPlan
{
    IReadOnlyList<TypeDiscoveryRegistration> Registrations { get; }

    void Freeze();

    void Commit(
        TypeDiscoveryCompilation compilation,
        ModuleConfigurationContext context,
        Action callbackStarting);

    void Release();
}

internal sealed record TypeDiscoveryRegistration(
    TypeQuery Query,
    Action<ModuleConfigurationContext, IReadOnlyList<BusinessTypeMatch>> Commit);

internal enum TypeDiscoveryPlanState
{
    Collecting,
    Frozen,
    Committed,
    Released
}
