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
        services.TryAddSingleton<ExecutionBehaviorPlanCache>();
        services.TryAddScoped<IExecutionPipeline, ExecutionPipeline>();
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
    /// <param name="key">
    /// A stable, case-sensitive key used for deterministic ordering, keyed DI resolution, and diagnostics.
    /// </param>
    /// <param name="behaviorType">
    /// A closed class implementing one or more <see cref="IExecutionBehavior{TInput,TResult}"/> contracts, or a
    /// two-parameter generic type definition implementing the matching open contract.
    /// </param>
    /// <param name="order">The behavior order. Lower values wrap higher values.</param>
    /// <param name="descriptorFilter">
    /// An optional descriptor-only predicate. It is evaluated when an immutable execution plan is first cached and
    /// must not depend on invocation-specific state.
    /// </param>
    /// <param name="lifetime">
    /// The dependency-injection lifetime used for behavior instances. Transient is the default. Scoped behaviors are
    /// resolved from the same scope as <see cref="IExecutionPipeline"/>.
    /// </param>
    /// <returns>The current guide for fluent configuration.</returns>
    /// <exception cref="ArgumentException">Thrown when the key or behavior type is invalid.</exception>
    public ModuleExecutionPipelineGuide AddBehavior(
        string key,
        Type behaviorType,
        int order = ExecutionBehaviorOrder.Application,
        Func<ExecutionDescriptor, bool>? descriptorFilter = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        var registration = ExecutionBehaviorRegistration.Create(
            key,
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
    /// A closed behavior class. Use the <see cref="AddBehavior(string,Type,int,Func{ExecutionDescriptor,bool}?,ServiceLifetime)"/>
    /// overload for an open generic type definition.
    /// </typeparam>
    /// <param name="key">A stable, case-sensitive behavior key.</param>
    /// <param name="order">The behavior order. Lower values wrap higher values.</param>
    /// <param name="descriptorFilter">An optional descriptor-only predicate.</param>
    /// <param name="lifetime">The dependency-injection lifetime used for behavior instances.</param>
    /// <returns>The current guide for fluent configuration.</returns>
    public ModuleExecutionPipelineGuide AddBehavior<TBehavior>(
        string key,
        int order = ExecutionBehaviorOrder.Application,
        Func<ExecutionDescriptor, bool>? descriptorFilter = null,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TBehavior : class
    {
        return AddBehavior(key, typeof(TBehavior), order, descriptorFilter, lifetime);
    }
}

/// <summary>
/// Configures the shared execution-pipeline module for one Monica host.
/// </summary>
public sealed class ModuleExecutionPipelineOption : ModuleOptions<ModuleExecutionPipeline>
{
    private readonly Dictionary<string, ExecutionBehaviorRegistration> _behaviorRegistrations =
        new(StringComparer.Ordinal);

    internal IReadOnlyCollection<ExecutionBehaviorRegistration> BehaviorRegistrations =>
        _behaviorRegistrations.Values;

    internal void AddBehavior(ExecutionBehaviorRegistration registration)
    {
        if (!_behaviorRegistrations.TryAdd(registration.Key, registration))
        {
            throw new InvalidOperationException(
                $"Execution behavior key '{registration.Key}' is already registered for this host. " +
                "Behavior keys must be unique and stable.");
        }
    }
}
