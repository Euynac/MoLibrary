using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core.Modularity.Models;

namespace Monica.Core.Modularity.Abstractions;

/// <summary>
/// Base type for Monica modules that participate only in the host-builder and service-registration lifecycle.
/// </summary>
public abstract class ModuleBase : IModule
{
    private MonicaApplication? _application;

    /// <summary>
    /// Gets the Monica application that owns this materialized module instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown before the module is bound to a host.</exception>
    protected MonicaApplication Application =>
        _application
            ?? throw new InvalidOperationException($"{GetType().Name} has not been bound to a Monica host.");

    /// <summary>
    /// Replaces the logger factory used to create future Monica composition loggers for the owning host.
    /// </summary>
    /// <remarks>
    /// Ownership transfers to the Monica application. Previously issued composition loggers remain valid until the
    /// application is disposed, even when a later module supplies a replacement factory.
    /// </remarks>
    /// <param name="factory">The host-specific logger factory.</param>
    protected void UseCompositionLoggerFactory(ILoggerFactory factory)
    {
        Application.ReplaceCompositionLoggerFactory(factory);
    }

    /// <summary>
    /// Schedules isolated module-owned CPU work that may overlap later serial composition callbacks.
    /// </summary>
    /// <param name="name">A stable name that is unique within the module.</param>
    /// <param name="work">The synchronous work Monica should execute on a bounded composition worker.</param>
    /// <param name="deadline">
    /// The latest composition checkpoint by which the work must complete. The default blocks service-registration
    /// completion, while allowing the greatest safe overlap with later composition callbacks.
    /// </param>
    /// <remarks>
    /// Schedule only CPU-bound work over immutable or module-owned state that no other callback consumes before the
    /// selected deadline. Scheduling is valid from the synchronous <c>ConfigureBuilder</c>, <c>ConfigureServices</c>,
    /// <c>IterateBusinessTypes</c>, and <c>PostConfigureServices</c> callbacks. Work scheduled during business-type
    /// iteration must use <see cref="ModuleCompositionWorkDeadline.BeforePostConfigureServices"/> or
    /// <see cref="ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion"/> because the earlier checkpoint
    /// has already passed. The callback must not mutate the host builder, service collection, module graph, provider,
    /// or shared static state. Runtime, I/O, optional, or fire-and-forget work belongs in the Generic Host lifecycle.
    /// Monica rejects asynchronous delegates and does not flow the caller's ambient execution context into workers.
    /// Capture required module-owned values explicitly. Monica owns execution and propagates every failure at the
    /// declared checkpoint.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or <paramref name="work"/> is an asynchronous delegate.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="work"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="deadline"/> is not a defined <see cref="ModuleCompositionWorkDeadline"/> value.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the module is not the materialized host-owned instance; scheduling does not occur synchronously
    /// on the same thread as that instance's <c>ConfigureBuilder</c>, <c>ConfigureServices</c>,
    /// <c>IterateBusinessTypes</c>, or <c>PostConfigureServices</c> callback; the name is duplicated; the selected
    /// deadline is unavailable from the active callback; or scheduling is nested from a composition worker.
    /// </exception>
    protected void ScheduleCompositionWork(
        string name,
        Action work,
        ModuleCompositionWorkDeadline deadline =
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion)
    {
        Application.Modules.ScheduleCompositionWork(this, name, work, commit: null, deadline: deadline);
    }

    /// <summary>
    /// Schedules isolated CPU work and a deterministic serial commit that publishes its completed result.
    /// </summary>
    /// <param name="name">A stable name that is unique within the module.</param>
    /// <param name="work">The synchronous work Monica should execute on a bounded composition worker.</param>
    /// <param name="commit">
    /// The synchronous, lightweight action Monica executes on the serial composition thread after every work item due
    /// at the checkpoint succeeds. The action may publish module-owned state or mutate the host service collection.
    /// </param>
    /// <param name="deadline">The checkpoint that waits for the worker and owns the serial commit.</param>
    /// <remarks>
    /// Monica executes successful commits in module registration and work submission order before the next serial
    /// composition phase begins. If any worker due at the checkpoint fails, no commit at that checkpoint runs. Commit
    /// actions must not perform I/O, launch asynchronous work, or schedule additional composition work.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is empty or either delegate is asynchronous.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="work"/> or <paramref name="commit"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="deadline"/> is not a defined <see cref="ModuleCompositionWorkDeadline"/> value.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown for the same ownership, callback, duplicate-name, and deadline violations as the worker-only overload.
    /// </exception>
    protected void ScheduleCompositionWork(
        string name,
        Action work,
        Action commit,
        ModuleCompositionWorkDeadline deadline =
            ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion)
    {
        ArgumentNullException.ThrowIfNull(commit);
        Application.Modules.ScheduleCompositionWork(this, name, work, commit, deadline);
    }

    /// <summary>
    /// Gets the resolved module key declared on the concrete module type.
    /// </summary>
    public ModuleKey ModuleKey => Application.Dependencies.ResolveModuleKey(GetType());

    /// <summary>
    /// Configures the host application builder.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    public virtual void ConfigureBuilder(IHostApplicationBuilder builder)
    {
    }

    /// <summary>
    /// Configures service registrations for the module.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Configures post-service registration actions after business-type iteration finishes.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public virtual void PostConfigureServices(IServiceCollection services)
    {
    }

    internal abstract void ConvertToRegisterRequest();

    /// <summary>
    /// Binds a materialized module to the host that owns its lifecycle.
    /// </summary>
    /// <param name="application">The owning Monica application.</param>
    internal void Bind(MonicaApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (_application is not null && !ReferenceEquals(_application, application))
        {
            throw new InvalidOperationException($"{GetType().Name} is already bound to another Monica host.");
        }

        _application = application;
    }
}


/// <summary>
/// Base abstract class for Monica modules.
/// </summary>
public abstract class ModuleBase<TModuleSelf, TModuleOption, TModuleGuide>(TModuleOption option) : ModuleBase, IModuleRequirementChecker, IModuleDependencyDeclarer
    where TModuleOption : ModuleOptions<TModuleSelf>, new() 
    where TModuleSelf : ModuleBase<TModuleSelf, TModuleOption, TModuleGuide>
    where TModuleGuide : ModuleGuide<TModuleSelf, TModuleOption, TModuleGuide>, new()
{
    public TModuleOption Option { get; } = option;
    public ILogger Logger => Option.Logger;

    /// <summary>
    /// Gets a configured option object for another module.
    /// </summary>
    /// <typeparam name="TSpecificModuleOption">The option type to retrieve.</typeparam>
    /// <returns>The configured option instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the option type cannot be resolved from a registered module.</exception>
    public TSpecificModuleOption GetOptions<TSpecificModuleOption>() where TSpecificModuleOption : IModuleOptionsBase, new()
    {
        var optionInterface = typeof(TSpecificModuleOption)
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IModuleOptionsBase<>));

        if (optionInterface == null)
            throw new InvalidOperationException($"{typeof(TSpecificModuleOption).Name} does not implement IModuleOptionsBase<T>.");

        var moduleType = optionInterface.GetGenericArguments()[0];
        Application.Modules.TryGetModuleRequestInfo(moduleType, out var context);
        
        if (context == null)
            throw new InvalidOperationException($"Module {moduleType.Name} is not registered.");

        return context.GetOrCreateFinalOption<TSpecificModuleOption>();
    }

    /// <summary>
    /// Gets the registration order for the module-owned host builder phase.
    /// </summary>
    /// <remarks>
    /// Override this when built-in builder behavior must execute at a specific point without relying on entry-only guide methods.
    /// </remarks>
    protected virtual int GetConfigureBuilderOrder()
    {
        return -1;
    }

    /// <summary>
    /// Gets the registration order for the module-owned service registration phase.
    /// </summary>
    /// <remarks>
    /// Override this when built-in service registration must execute at a specific point without relying on entry-only guide methods.
    /// </remarks>
    protected virtual int GetConfigureServicesOrder()
    {
        return -1;
    }

    /// <summary>
    /// Gets the registration order for the module-owned post-service phase.
    /// </summary>
    /// <remarks>
    /// Override this when built-in post-configuration must execute at a specific point without relying on entry-only guide methods.
    /// </remarks>
    protected virtual int GetPostConfigureServicesOrder()
    {
        return -1;
    }

    internal override void ConvertToRegisterRequest()
    {
        var guide = Application.CreateGuide<TModuleGuide>(ModuleKey);

        guide.ConfigureBuilder(context =>
        {
            ConfigureBuilder(context.HostApplicationBuilder);
        }, GetConfigureBuilderOrder());

        guide.ConfigureServices(context =>
        {
            ConfigureServices(context.Services);
        }, GetConfigureServicesOrder());

        guide.PostConfigureServices(context =>
        {
            PostConfigureServices(context.Services);
        }, GetPostConfigureServicesOrder());
    }

    public void CheckRequiredMethod(string methodName, string? errorDetail = null)
    {
        Application.CreateGuide<TModuleGuide>(ModuleKey).CheckRequiredMethod(methodName, errorDetail);
    }

    public virtual void ClaimDependencies()
    {
    }

    [MustUseReturnValue]
    protected TOtherModuleGuide DependsOnModule<TOtherModuleGuide>()
        where TOtherModuleGuide : ModuleGuide, new()
    {
        var dependencyGuide = Application.CreateGuide<TOtherModuleGuide>(ModuleKey);
        Application.Dependencies.AddDependency(ModuleKey, dependencyGuide.GetTargetModuleKey());
        return dependencyGuide;
    }
}


public interface IModuleDependencyDeclarer
{
    /// <summary>
    /// Declares dependent modules and optionally configures them.
    /// The option instance available here may not be final because automatically registered configuration from other modules has not been merged yet.
    /// </summary>
    void ClaimDependencies();
}
