using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monica.Core.Execution;
using Monica.Core.Execution.Models.Internal;
using Monica.Core.Execution.Services;
using Monica.Core.Modularity;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Models;

// ReSharper disable once CheckNamespace
namespace Monica.Modules;

/// <summary>
/// Provides host-bound registration for Monica's typed execution pipeline.
/// </summary>
public static class ModuleExecutionPipelineBuilderExtensions
{
    extension(IMonicaBuilder builder)
    {
        /// <summary>
        /// Registers the shared execution-pipeline kernel for the current Monica host.
        /// </summary>
        /// <param name="configure">Optional module configuration.</param>
        /// <returns>A guide used to register ordered execution behaviors.</returns>
        public ModuleExecutionPipelineGuide AddExecutionPipeline(
            Action<ModuleExecutionPipelineOption>? configure = null)
        {
            return builder.AddModule<
                ModuleExecutionPipeline,
                ModuleExecutionPipelineOption,
                ModuleExecutionPipelineGuide>(configure);
        }
    }
}

/// <summary>
/// Registers the shared typed execution pipeline and its host-owned behavior catalog.
/// </summary>
[ModuleKey(BuiltInModuleKey.ExecutionPipeline)]
public sealed class ModuleExecutionPipeline(ModuleExecutionPipelineOption option)
    : ModuleBase<ModuleExecutionPipeline, ModuleExecutionPipelineOption, ModuleExecutionPipelineGuide>(option)
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        var registrations = option.BehaviorRegistrations.ToArray();

        foreach (var registration in registrations)
        {
            services.Add(registration.CreateServiceDescriptor());
        }

        services.TryAddSingleton<IReadOnlyList<ExecutionBehaviorRegistration>>(registrations);
        services.TryAddSingleton(_ =>
            new ExecutionBehaviorServiceRegistrationValidator(registrations, services));
        services.TryAddSingleton<ExecutionBehaviorPlanCache>();
        services.TryAddScoped<IExecutionPipeline, ExecutionPipeline>();
    }

    /// <inheritdoc />
    public override void PostConfigureServices(IServiceCollection services)
    {
        foreach (var registration in option.BehaviorRegistrations)
        {
            registration.ValidateExclusiveServiceRegistration(services);
        }
    }
}

/// <summary>
/// Configures ordered, descriptor-filtered behaviors for the shared execution pipeline.
/// </summary>
public sealed class ModuleExecutionPipelineGuide
    : ModuleGuide<ModuleExecutionPipeline, ModuleExecutionPipelineOption, ModuleExecutionPipelineGuide>
{
    /// <summary>
    /// Adds an execution behavior to the current host.
    /// </summary>
    /// <param name="behaviorType">
    /// A closed class implementing one or more <see cref="IExecutionBehavior{TInput,TResult}"/> contracts, or a
    /// two-parameter generic type definition implementing the matching open contract.
    /// </param>
    /// <param name="order">
    /// The behavior order. Lower values wrap higher values. Equal-order behaviors are sorted by implementation
    /// type only for reproducibility and must remain semantically independent; use distinct orders when relative
    /// nesting matters.
    /// </param>
    /// <param name="descriptorFilter">
    /// An optional descriptor-only predicate. It is evaluated when an immutable execution plan is first cached and
    /// must not depend on invocation-specific state.
    /// </param>
    /// <param name="lifetime">
    /// The dependency-injection lifetime used for behavior instances. Transient is the default. Scoped behaviors are
    /// resolved from the same scope as <see cref="IExecutionPipeline"/>.
    /// </param>
    /// <returns>The current guide for fluent configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when the behavior type is invalid.</exception>
    /// <remarks>
    /// This registration owns the behavior's dependency-injection service descriptor. Do not register the same
    /// implementation type separately in the host service collection.
    /// </remarks>
    public ModuleExecutionPipelineGuide AddBehavior(
        Type behaviorType,
        int order = ExecutionBehaviorOrder.Application,
        Func<ExecutionDescriptor, bool>? descriptorFilter = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var registration = ExecutionBehaviorRegistration.Create(
            behaviorType,
            order,
            descriptorFilter,
            lifetime);

        ConfigureModuleOption(
            moduleOption => moduleOption.AddBehavior(registration),
            secondKey: Guid.NewGuid().ToString("N"),
            duplicateBehavior: ModuleConfigurationDuplicateBehavior.SilentIdempotent);
        return this;
    }

    /// <summary>
    /// Adds an execution behavior to the current host.
    /// </summary>
    /// <typeparam name="TBehavior">
    /// A closed behavior class. Use the <see cref="AddBehavior(Type,int,Func{ExecutionDescriptor,bool}?,ServiceLifetime)"/>
    /// overload for an open generic type definition.
    /// </typeparam>
    /// <param name="order">
    /// The behavior order. Lower values wrap higher values. Equal-order behaviors must not depend on their relative
    /// nesting because the implementation type is used only as a deterministic tie-breaker.
    /// </param>
    /// <param name="descriptorFilter">An optional descriptor-only predicate.</param>
    /// <param name="lifetime">The dependency-injection lifetime used for behavior instances.</param>
    /// <returns>The current guide for fluent configuration.</returns>
    public ModuleExecutionPipelineGuide AddBehavior<TBehavior>(
        int order = ExecutionBehaviorOrder.Application,
        Func<ExecutionDescriptor, bool>? descriptorFilter = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TBehavior : class
    {
        return AddBehavior(typeof(TBehavior), order, descriptorFilter, lifetime);
    }
}

/// <summary>
/// Configures the shared execution-pipeline module for one Monica host.
/// </summary>
public sealed class ModuleExecutionPipelineOption : ModuleOptions<ModuleExecutionPipeline>
{
    private readonly Dictionary<Type, ExecutionBehaviorRegistration> _behaviorRegistrations = [];

    internal IReadOnlyCollection<ExecutionBehaviorRegistration> BehaviorRegistrations =>
        _behaviorRegistrations.Values;

    internal void AddBehavior(ExecutionBehaviorRegistration registration)
    {
        if (!_behaviorRegistrations.TryAdd(registration.ImplementationType, registration))
        {
            throw new InvalidOperationException(
                $"Execution behavior '{registration.ImplementationType.FullName}' is already registered for this host. " +
                "Register each behavior implementation type only once.");
        }
    }
}
