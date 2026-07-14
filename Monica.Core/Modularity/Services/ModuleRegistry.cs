using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services.Support;
using Monica.Core.Modularity.State;
using Monica.Tool.Extensions;

namespace Monica.Core.Modularity.Services;

/// <summary>
/// Central registry that drives the module registration lifecycle and manages module configuration and initialization.
/// </summary>
public sealed class ModuleRegistry(MonicaApplication application)
{
    private readonly ModuleRegistryState _state = new();
    private bool _hasStarted;
    private bool _isSealed;

    /// <summary>
    /// Gets a read-only view of registration errors owned by this Monica host.
    /// </summary>
    /// <remarks>
    /// The view follows the host lifecycle but cannot be used to mutate registry state.
    /// </remarks>
    public IReadOnlyList<ModuleRegistrationError> RegistrationErrors => _state.RegistrationErrors;

    public ILogger Logger => application.CreateLogger(typeof(ModuleRegistry));

    /// <summary>
    /// Gets a read-only view of module snapshots captured after successful registration.
    /// </summary>
    /// <remarks>
    /// The view follows the host lifecycle but cannot be used to add or remove snapshots.
    /// </remarks>
    public IReadOnlyList<ModuleRuntimeSnapshot> RuntimeSnapshots => _state.RuntimeSnapshots;

    /// <summary>
    /// Registration information for every module type that has been registered.
    /// </summary>
    internal IReadOnlyDictionary<Type, ModuleRegistrationState> Registrations => _state.Registrations;

    /// <summary>
    /// Attempts to retrieve the ModuleRequestInfo for a specified module type.
    /// </summary>
    /// <param name="type">The type of the module to retrieve information for.</param>
    /// <param name="requestInfo"></param>
    /// <returns>The ModuleRequestInfo if found; otherwise, null.</returns>
    internal bool TryGetModuleRequestInfo(Type type, [NotNullWhen(true)] out ModuleRegistrationState? requestInfo)
    {
        return _state.TryGetRegistration(type, out requestInfo);
    }

    /// <summary>
    /// Determines whether the current Monica host registered the specified module type.
    /// </summary>
    /// <param name="moduleType">The module type to inspect.</param>
    /// <returns><see langword="true"/> when the module belongs to this host; otherwise, <see langword="false"/>.</returns>
    public bool IsRegistered(Type moduleType)
    {
        ArgumentNullException.ThrowIfNull(moduleType);
        return _state.TryGetRegistration(moduleType, out _);
    }

    /// <summary>
    /// Gets all keyed service keys registered by the specified module.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <returns>The keyed service keys for the module, or an empty set if the module is unknown.</returns>
    public IReadOnlySet<string> GetKeyedServiceKeys(Type moduleType)
    {
        return TryGetModuleRequestInfo(moduleType, out var info)
            ? info.KeyedServiceKeys.ToFrozenSet()
            : FrozenSet<string>.Empty;
    }

    /// <summary>
    /// Adds module registration information for a module type.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <param name="registerInfo">The registration information.</param>
    internal void AddModuleRegisterContext(Type moduleType, ModuleRegistrationState registerInfo)
    {
        EnsureCompositionIsOpen();

        if (!_state.TryAddRegistration(moduleType, registerInfo))
        {
            throw new ModuleRegistrationException($"Module type {moduleType.FullName} is already registered.");
        }

        // Start overall profiling when the first module is registered.
        if (Registrations.Count == 1)
        {
            application.Profiling.StartModuleSystem();
            Logger.LogInformation("Module system initialization started");
        }
    }

    /// <summary>
    /// Records an error produced by the host's module registration lifecycle.
    /// </summary>
    /// <param name="error">The error to record.</param>
    internal void AddRegistrationError(ModuleRegistrationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _state.AddRegistrationError(error);
    }

    /// <summary>
    /// Registers services for all currently registered modules.
    /// This method must run before `builder.Build()`.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    internal void RegisterServices(IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (_hasStarted)
        {
            throw new InvalidOperationException("The Monica module graph has already been applied to its host.");
        }

        _hasStarted = true;
        var services = builder.Services;


        // Clear any error state from a previous registration run.
        _state.ClearRegistrationErrors();
        
        application.Profiling.StartPhase(nameof(ModulePhase.ClaimDependencies));
        // 1. First pass: let modules declare dependencies.

        while (Registrations.Where(p=>p.Value.ModulePhase == ModulePhase.None).ToList() is {Count: > 0} list)
        {
            foreach (var (moduleType, info) in list.OrderBy(p => p.Value.Order).Select(p => p).ToList())
            {
                info.StartModulePhase(ModulePhase.ClaimDependencies);
                try
                {
                    var option = info.CreateCurrentModuleOption();
                    if (Activator.CreateInstance(moduleType, option) is ModuleBase moduleInstance)
                    {
                        moduleInstance.Bind(application);
                        ((IModuleDependencyDeclarer)moduleInstance).ClaimDependencies();
                    }
                }
                catch (Exception ex)
                {
                    application.Errors.RecordModuleError(moduleType, ex,
                        ModulePhase.ClaimDependencies, ModuleRegistrationErrorType.InitializationError);
                }
                finally
                {
                    info.EndModulePhase(ModulePhase.ClaimDependencies);
                }
            }
        }
       

        application.Profiling.StopPhase(nameof(ModulePhase.ClaimDependencies));

        // 1.1 Refresh module ordering after all dependencies have been declared.
        application.Errors.ValidateDependencyGraph();
        application.Dependencies.RefreshAllModuleOrders();

        var snapshots = new List<ModuleRuntimeSnapshot>();

        // 2. Materialize the final configuration objects for each module.
        application.Profiling.StartPhase(nameof(ModulePhase.InitFinalConfigures));
        foreach (var (moduleType, info) in Registrations.Where(p => p.Value.ModulePhase == ModulePhase.ClaimDependencies).OrderBy(p => p.Value.Order))
        {
            try
            {
                info.StartModulePhase(ModulePhase.InitFinalConfigures);
                // Finalize the module configuration objects.
                info.InitFinalConfigures();
                info.EndModulePhase(ModulePhase.InitFinalConfigures);
            }
            catch (Exception ex)
            {
                throw ex.CreateException(Logger,
                    $"Module {moduleType.GetCleanFullName()} failed during {nameof(ModuleRegistrationState.InitFinalConfigures)}.");
            }
        }
        application.ModuleStates.Init();
        ValidateWebModuleCompatibility(builder);
        // 2.1 Validate required configuration for every initialized module.
        application.Errors.ValidateModuleRequirements(Registrations.Where(p => p.Value.ModulePhase == ModulePhase.InitFinalConfigures).ToDictionary());
        application.Profiling.StopPhase(nameof(ModulePhase.InitFinalConfigures));

        _isSealed = true;
        services.AddSingleton<MonicaApplication>(_ => application);
        services.AddSingleton<IMonicaApplicationOptions>(application.Application);
        services.AddSingleton<IMonicaModuleSystemOptions>(application.ModuleSystem);
        foreach (var optionType in Registrations.Values
                     .Select(info => info.ModuleOptionType)
                     .Distinct())
        {
            RegisterModuleOptionContext(services, optionType);
        }

        // 2.2 Execute builder and service registrations.
        application.Profiling.StartPhase(nameof(ModulePhase.ConfigureBuilder) + nameof(ModulePhase.ConfigureServices));
        foreach (var (moduleType, info) in Registrations.Where(p => p.Value.ModulePhase == ModulePhase.InitFinalConfigures).OrderBy(p => p.Value.Order))
        {
            // Run builder configuration first.
            info.StartModulePhase(ModulePhase.ConfigureBuilder);

            // Execute queued builder configuration requests.
            foreach (var request in info.DeduplicateRequests(
                info.RegisterRequests
                    .Where(p => p.RequestMethod == ModulePhase.ConfigureBuilder)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleConfigurationContext(services, null, builder, info));
                }
                catch (Exception ex)
                {
                    application.Errors.RecordRequestError(moduleType, request, ex);
                }
            }

            info.EndModulePhase(ModulePhase.ConfigureBuilder);


            // Then run service registrations.
            info.StartModulePhase(ModulePhase.ConfigureServices);

            // Execute queued service configuration requests.
            foreach (var request in info.DeduplicateRequests(
                info.RegisterRequests
                    .Where(p => p.RequestMethod == ModulePhase.ConfigureServices)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleConfigurationContext(services, null, builder, info));
                }
                catch (Exception ex)
                {
                    application.Errors.RecordRequestError(moduleType, request, ex);
                }
            }

            info.EndModulePhase(ModulePhase.ConfigureServices);
            snapshots.Add(new ModuleRuntimeSnapshot(application, info.ModuleSingleton!, info));
        }
        application.Profiling.StopPhase(nameof(ModulePhase.ConfigureBuilder) + nameof(ModulePhase.ConfigureServices));


        // 3. Allow modules to inspect and transform discovered business types.
        application.Profiling.StartPhase(nameof(ModulePhase.IterateBusinessTypes));
        var businessTypes = application.TypeFinder.GetTypes()
            .Where(static type => !type.IsDefined(typeof(ExcludeFromBusinessTypeDiscoveryAttribute), inherit: false));
        var needToIterate = false;
        foreach (var module in snapshots.Where(p => p.RegisterInfo.ModulePhase == ModulePhase.ConfigureServices))
        {
            if (module.ModuleInstance is not IBusinessTypeIterator iterateModule) continue;
            needToIterate = true;
            businessTypes = iterateModule.IterateBusinessTypes(businessTypes);
        }

        if (needToIterate)
        {
            try
            {
                _ = businessTypes.ToList();
            }
            catch (Exception ex)
            {
                throw ex.CreateException(Logger, "Business-type iteration failed during Monica module registration.");
            }
        }
        application.Profiling.StopPhase(nameof(ModulePhase.IterateBusinessTypes));


        // 4. Execute post-service configuration hooks.
        application.Profiling.StartPhase(nameof(ModulePhase.PostConfigureServices));
        foreach (var module in snapshots)
        {
            module.RegisterInfo.StartModulePhase(ModulePhase.PostConfigureServices);
            // Execute queued post-configuration requests.
            foreach (var request in module.RegisterInfo.DeduplicateRequests(
                module.RegisterInfo.RegisterRequests
                    .Where(p => p.RequestMethod == ModulePhase.PostConfigureServices)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleConfigurationContext(services, null, builder, module.RegisterInfo));
                }
                catch (Exception ex)
                {
                    application.Errors.RecordRequestError(module.ModuleType, request, ex);
                }
            }

            module.RegisterInfo.EndModulePhase(ModulePhase.PostConfigureServices);
        }
        application.Profiling.StopPhase(nameof(ModulePhase.PostConfigureServices));
        _state.AddRuntimeSnapshots(snapshots);
        application.Errors.RaiseModuleErrors();
    }

    /// <summary>
    /// Clears all lifecycle data so an isolated host fixture can be rebuilt.
    /// </summary>
    internal void Clear()
    {
        _state.Clear();
        _hasStarted = false;
        _isSealed = false;
    }

    /// <summary>
    /// Rejects guide mutations after the module graph has been validated and sealed.
    /// </summary>
    internal void EnsureCompositionIsOpen()
    {
        if (_isSealed)
        {
            throw new InvalidOperationException(
                "The Monica module graph is sealed. Register and configure modules only inside AddMonica(...).");
        }
    }

    private void RegisterModuleOptionContext(IServiceCollection services, Type optionType)
    {
        var postConfigureType = typeof(IPostConfigureOptions<>).MakeGenericType(optionType);
        var implementationType = typeof(ModuleOptionsContextPostConfigure<>).MakeGenericType(optionType);
        var implementation = Activator.CreateInstance(implementationType, application)
            ?? throw new InvalidOperationException(
                $"Could not create the Monica option context binder for {optionType.GetCleanFullName()}.");
        services.AddSingleton(postConfigureType, implementation);
    }

    /// <summary>
    /// Configures the application pipeline for the registered modules.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="order">The ordering split point.</param>
    /// <param name="afterGivenOrder">Whether to configure items after the given order instead of before it.</param>
    internal void ConfigApplicationPipeline(IApplicationBuilder app, int order, bool afterGivenOrder)
    {
        var phaseName = afterGivenOrder ? $"{nameof(ConfigApplicationPipeline)}_After_{order}" : $"{nameof(ConfigApplicationPipeline)}_Before_{order}";
        application.Profiling.StartPhase(phaseName);

        Func<ModuleConfigurationRequest, bool> filter = afterGivenOrder ? request => request.Order > order : request => request.Order <= order;
        // Execute application builder requests in priority order.
        foreach (var module in RuntimeSnapshots.Where(p =>
                     p.ModuleInstance is IWebModule &&
                     !p.RegisterInfo.IsDowngradedFromWebModule &&
                     p.RegisterInfo.ModulePhase is (ModulePhase.PostConfigureServices or ModulePhase.ConfigureApplicationBuilder)))
        {
            module.RegisterInfo.StartModulePhase(ModulePhase.ConfigureApplicationBuilder);

            foreach (var request in module.RegisterInfo.DeduplicateRequests(
                module.RegisterInfo.RegisterRequests
                    .Where(p => p.RequestMethod == ModulePhase.ConfigureApplicationBuilder)
                    .Where(filter)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleConfigurationContext(null, app, null, module.RegisterInfo));
                }
                catch (Exception ex)
                {
                    application.Errors.RecordRequestError(module.ModuleType, request, ex);
                }
            }
            
            module.RegisterInfo.EndModulePhase(ModulePhase.ConfigureApplicationBuilder);

        }

        application.Profiling.StopPhase(phaseName);
    }

    /// <summary>
    /// Configures endpoints for the registered modules.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal void ConfigEndpoints(IApplicationBuilder app)
    {
        application.Profiling.StartPhase(nameof(ModulePhase.ConfigureEndpoints));

        // Execute endpoint configuration requests in priority order.
        foreach (var module in RuntimeSnapshots.Where(p =>
                     p.ModuleInstance is IWebModule &&
                     !p.RegisterInfo.IsDowngradedFromWebModule &&
                     p.RegisterInfo.ModulePhase == ModulePhase.ConfigureApplicationBuilder))
        {
            module.RegisterInfo.StartModulePhase(ModulePhase.ConfigureEndpoints);

            foreach (var request in module.RegisterInfo.DeduplicateRequests(
                module.RegisterInfo.RegisterRequests
                    .Where(p => p.RequestMethod == ModulePhase.ConfigureEndpoints)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleConfigurationContext(null, app, null, module.RegisterInfo));
                }
                catch (Exception ex)
                {
                    application.Errors.RecordRequestError(module.ModuleType, request, ex);
                }
            }

            module.RegisterInfo.EndModulePhase(ModulePhase.ConfigureEndpoints);
        }

        application.Profiling.StopPhase(nameof(ModulePhase.ConfigureEndpoints));
        application.Profiling.StopModuleSystem();

        if (application.ModuleSystem.EnableSummaryLog)
        {
            // Log performance summary details
            Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}",
                application.Profiling.GetPerformanceSummary());
            Logger.LogInformation("Module system register order summary:\n{Order}",
                application.Dependencies.GetModuleRegistrationSummary());
        }
       

        application.Errors.RaiseModuleErrors();
    }

    /// <summary>
    /// Gets all module snapshots that are providers for a specific target module.
    /// </summary>
    /// <param name="targetModuleKey">The ModuleKey of the target module to find providers for</param>
    /// <returns>A detached list of runtime snapshots for modules that provide the target module.</returns>
    public IReadOnlyList<ModuleRuntimeSnapshot> GetModuleProviders(ModuleKey targetModuleKey)
    {
        return Array.AsReadOnly(RuntimeSnapshots
            .Where(snapshot => snapshot.ModuleInstance is IModuleProvider provider
                               && provider.ProvidesFor == targetModuleKey)
            .ToArray());
    }

    /// <summary>
    /// Gets all module providers of a specific type for a target module.
    /// </summary>
    /// <typeparam name="TProvider">The provider interface type</typeparam>
    /// <param name="targetModuleKey">The ModuleKey of the target module to find providers for</param>
    /// <returns>A detached, read-only list of provider instances.</returns>
    public IReadOnlyList<TProvider> GetModuleProviders<TProvider>(ModuleKey targetModuleKey)
        where TProvider : IModuleProvider
    {
        return Array.AsReadOnly(RuntimeSnapshots
            .Where(snapshot => snapshot.ModuleInstance is TProvider provider
                        && provider.ProvidesFor == targetModuleKey)
            .Select(snapshot => (TProvider)snapshot.ModuleInstance)
            .ToArray());
    }

    private void ValidateWebModuleCompatibility(IHostApplicationBuilder builder)
    {
        if (builder is WebApplicationBuilder)
        {
            return;
        }

        foreach (var (moduleType, info) in Registrations
                     .Where(entry => entry.Value.ModulePhase == ModulePhase.InitFinalConfigures)
                     .OrderBy(entry => entry.Value.Order))
        {
            if (info.ModuleSingleton is not IWebModule webModule)
            {
                continue;
            }

            if (webModule.CanDowngradeToNonWebModule())
            {
                info.IsDowngradedFromWebModule = true;
                Logger.LogInformation(
                    "Module {ModuleName} is running in downgraded non-web mode because the current host is {HostBuilderType}.",
                    moduleType.Name,
                    builder.GetType().FullName);
                continue;
            }

            var moduleKey = application.Dependencies.ResolveModuleKey(moduleType);
            application.Errors.RecordHostCompatibilityError(
                moduleType,
                $"Module {moduleType.Name} ({moduleKey}) requires an ASP.NET Core host and cannot downgrade to a non-web module.");
        }

        application.Errors.RaiseModuleErrors();
    }
}
