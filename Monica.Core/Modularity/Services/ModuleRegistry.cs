using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
    private readonly ModuleCompositionState _composition = new();
    private readonly object _compositionCallbackGate = new();
    private readonly ModuleRegistryState _state = new();
    private ModuleRegistrationState? _activeCompositionCallback;
    private int _activeCompositionCallbackThreadId;
    private ModuleCompositionWorkScheduler? _compositionWork;
    private bool _compositionWorkRecorded;
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
        _composition.Initialize(builder);
        _compositionWork = new ModuleCompositionWorkScheduler(
            application.ModuleSystem.MaxConcurrentCompositionWorkItems);
        var services = builder.Services;

        try
        {
            _state.ClearRegistrationErrors();
            DeclareDependencies();
            application.Errors.ValidateDependencyGraph();
            application.Dependencies.RefreshAllModuleOrders();

            var registrations = MaterializeModules(builder);
            RegisterCoreServices(services);
            var snapshots = ExecuteBuilderAndServiceConfiguration(builder, services, registrations);
            ReachCompositionWorkCheckpoint(ModuleCompositionWorkDeadline.BeforeBusinessTypeIteration);

            IterateBusinessTypes(snapshots);
            ReachCompositionWorkCheckpoint(ModuleCompositionWorkDeadline.BeforePostConfigureServices);

            ExecutePostConfigureServices(builder, services, snapshots);
            ReachCompositionWorkCheckpoint(ModuleCompositionWorkDeadline.BeforeServiceRegistrationCompletion);
            _ = FinalizeCompositionWork(abort: false);

            _state.AddRuntimeSnapshots(snapshots);
            if (builder is WebApplicationBuilder)
            {
                // Web composition remains open until MapMonica() has configured every endpoint.
                application.Errors.RaiseModuleErrors();
            }
            else
            {
                CompleteComposition(ModuleCompositionCompletionPoint.ServiceRegistration);
            }
        }
        catch (Exception exception)
        {
            HandleRegistrationFailure(exception);
            throw;
        }
    }

    private void DeclareDependencies()
    {
        application.Profiling.StartPhase(nameof(ModulePhase.ClaimDependencies));
        try
        {
            while (Registrations.Where(static entry => entry.Value.ModulePhase == ModulePhase.None).ToList()
                   is { Count: > 0 } pending)
            {
                foreach (var (moduleType, info) in pending.OrderBy(static entry => entry.Value.Order))
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
                    catch (Exception exception)
                    {
                        application.Errors.RecordModuleError(
                            moduleType,
                            exception,
                            ModulePhase.ClaimDependencies,
                            ModuleRegistrationErrorType.InitializationError);
                    }
                    finally
                    {
                        info.EndModulePhase(ModulePhase.ClaimDependencies);
                    }
                }
            }
        }
        finally
        {
            application.Profiling.StopPhase(nameof(ModulePhase.ClaimDependencies));
        }
    }

    private IReadOnlyList<ModuleRegistrationState> MaterializeModules(IHostApplicationBuilder builder)
    {
        var registrations = Registrations.Values
            .Where(static info => info.ModulePhase == ModulePhase.ClaimDependencies)
            .OrderBy(static info => info.Order)
            .ToArray();

        application.Profiling.StartPhase(nameof(ModulePhase.InitFinalConfigures));
        try
        {
            foreach (var info in registrations)
            {
                info.StartModulePhase(ModulePhase.InitFinalConfigures);
                try
                {
                    info.InitFinalConfigures();
                }
                catch (Exception exception)
                {
                    throw exception.CreateException(
                        Logger,
                        $"Module {info.ModuleType.GetCleanFullName()} failed during {nameof(ModuleRegistrationState.InitFinalConfigures)}.");
                }
                finally
                {
                    info.EndModulePhase(ModulePhase.InitFinalConfigures);
                }
            }

            application.ModuleStates.Init();
            ValidateWebModuleCompatibility(builder);
            application.Errors.ValidateModuleRequirements(registrations.ToDictionary(static info => info.ModuleType));
            return registrations
                .Where(static info => info.ModulePhase == ModulePhase.InitFinalConfigures)
                .ToArray();
        }
        finally
        {
            application.Profiling.StopPhase(nameof(ModulePhase.InitFinalConfigures));
        }
    }

    private void RegisterCoreServices(IServiceCollection services)
    {
        _isSealed = true;
        services.AddSingleton<MonicaApplication>(_ => application);
        services.AddSingleton<IMonicaApplicationOptions>(application.Application);
        services.AddSingleton<IMonicaModuleSystemOptions>(application.ModuleSystem);
        RegisterCompositionStartupValidation(services);

        foreach (var optionType in Registrations.Values
                     .Select(static info => info.ModuleOptionType)
                     .Distinct())
        {
            RegisterModuleOptionContext(services, optionType);
        }
    }

    private IReadOnlyList<ModuleRuntimeSnapshot> ExecuteBuilderAndServiceConfiguration(
        IHostApplicationBuilder builder,
        IServiceCollection services,
        IReadOnlyList<ModuleRegistrationState> registrations)
    {
        var snapshots = new List<ModuleRuntimeSnapshot>(registrations.Count);
        var phaseName = nameof(ModulePhase.ConfigureBuilder) + nameof(ModulePhase.ConfigureServices);
        application.Profiling.StartPhase(phaseName);
        try
        {
            foreach (var info in registrations)
            {
                ExecuteConfigurationRequests(builder, services, info, ModulePhase.ConfigureBuilder);
                ExecuteConfigurationRequests(builder, services, info, ModulePhase.ConfigureServices);
                snapshots.Add(new ModuleRuntimeSnapshot(application, info.ModuleSingleton!, info));
            }

            return snapshots;
        }
        finally
        {
            application.Profiling.StopPhase(phaseName);
        }
    }

    private void ExecuteConfigurationRequests(
        IHostApplicationBuilder builder,
        IServiceCollection services,
        ModuleRegistrationState registration,
        ModulePhase phase)
    {
        registration.StartModulePhase(phase);
        try
        {
            foreach (var request in registration.DeduplicateRequests(
                         registration.RegisterRequests
                             .Where(request => request.RequestMethod == phase)
                             .OrderBy(static request => request.Order)))
            {
                try
                {
                    BeginCompositionCallback(registration);
                    request.ConfigureContext?.Invoke(
                        new ModuleConfigurationContext(services, null, builder, registration));
                }
                catch (Exception exception)
                {
                    application.Errors.RecordRequestError(registration.ModuleType, request, exception);
                }
                finally
                {
                    EndCompositionCallback(registration);
                }
            }
        }
        finally
        {
            registration.EndModulePhase(phase);
        }
    }

    private void BeginCompositionCallback(ModuleRegistrationState registration)
    {
        lock (_compositionCallbackGate)
        {
            if (_activeCompositionCallback is not null)
            {
                throw new InvalidOperationException("Module composition callbacks cannot overlap on the serial control plane.");
            }

            _activeCompositionCallback = registration;
            _activeCompositionCallbackThreadId = Environment.CurrentManagedThreadId;
        }
    }

    private void EndCompositionCallback(ModuleRegistrationState registration)
    {
        lock (_compositionCallbackGate)
        {
            if (ReferenceEquals(_activeCompositionCallback, registration))
            {
                _activeCompositionCallback = null;
                _activeCompositionCallbackThreadId = 0;
            }
        }
    }

    private void IterateBusinessTypes(IReadOnlyList<ModuleRuntimeSnapshot> snapshots)
    {
        application.Profiling.StartPhase(nameof(ModulePhase.IterateBusinessTypes));
        try
        {
            var businessTypes = application.TypeFinder.GetTypes()
                .Where(static type => !type.IsDefined(
                    typeof(ExcludeFromBusinessTypeDiscoveryAttribute),
                    inherit: false));
            var hasIterators = false;
            foreach (var snapshot in snapshots)
            {
                if (snapshot.ModuleInstance is not IBusinessTypeIterator iterator)
                {
                    continue;
                }

                snapshot.RegisterInfo.SetModulePhase(ModulePhase.IterateBusinessTypes);
                hasIterators = true;
                businessTypes = iterator.IterateBusinessTypes(businessTypes);
            }

            if (hasIterators)
            {
                _ = businessTypes.ToList();
            }
        }
        catch (Exception exception)
        {
            throw exception.CreateException(Logger, "Business-type iteration failed during Monica module registration.");
        }
        finally
        {
            application.Profiling.StopPhase(nameof(ModulePhase.IterateBusinessTypes));
        }
    }

    /// <summary>
    /// Clears all lifecycle data so an isolated host fixture can be rebuilt.
    /// </summary>
    internal void Clear()
    {
        _compositionWork?.Dispose();
        _compositionWork = null;
        _compositionWorkRecorded = false;
        lock (_compositionCallbackGate)
        {
            _activeCompositionCallback = null;
            _activeCompositionCallbackThreadId = 0;
        }
        _state.Clear();
        _composition.Clear();
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

    /// <summary>
    /// Validates module ownership and phase rules before handing isolated work to the scheduler.
    /// </summary>
    internal void ScheduleCompositionWork(
        ModuleBase owner,
        string name,
        Action work,
        ModuleCompositionWorkDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(work);

        if (work.GetInvocationList().Any(static callback =>
                callback.Method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false)))
        {
            throw new ArgumentException(
                "Module composition work must be synchronous. Use the Generic Host lifecycle for asynchronous work.",
                nameof(work));
        }

        if (!Enum.IsDefined(deadline))
        {
            throw new ArgumentOutOfRangeException(nameof(deadline), deadline, "Unknown composition work deadline.");
        }

        var moduleType = owner.GetType();
        if (!TryGetModuleRequestInfo(moduleType, out var info)
            || !ReferenceEquals(info.ModuleSingleton, owner))
        {
            throw new InvalidOperationException(
                $"Only the materialized host-owned {moduleType.Name} instance can schedule composition work.");
        }

        lock (_compositionCallbackGate)
        {
            if (!ReferenceEquals(_activeCompositionCallback, info)
                || _activeCompositionCallbackThreadId != Environment.CurrentManagedThreadId
                || info.ModulePhase is not (ModulePhase.ConfigureBuilder
                    or ModulePhase.ConfigureServices
                    or ModulePhase.PostConfigureServices))
            {
                throw new InvalidOperationException(
                    $"Module {moduleType.Name} can schedule composition work only while its synchronous " +
                    "ConfigureBuilder, ConfigureServices, or PostConfigureServices callback is executing.");
            }
        }

        var scheduler = _compositionWork
            ?? throw new InvalidOperationException("Module composition work is no longer available for this host.");
        scheduler.Schedule(moduleType, info.Order, name, info.ModulePhase, deadline, work);
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

    private void RegisterCompositionStartupValidation(IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<ModuleCompositionStartupOptions>>(
            new ModuleCompositionStartupValidator(application));
        services.AddOptions<ModuleCompositionStartupOptions>().ValidateOnStart();
    }

    private void ExecutePostConfigureServices(
        IHostApplicationBuilder builder,
        IServiceCollection services,
        IReadOnlyList<ModuleRuntimeSnapshot> snapshots)
    {
        application.Profiling.StartPhase(nameof(ModulePhase.PostConfigureServices));
        try
        {
            foreach (var module in snapshots)
            {
                ExecuteConfigurationRequests(
                    builder,
                    services,
                    module.RegisterInfo,
                    ModulePhase.PostConfigureServices);
            }
        }
        finally
        {
            application.Profiling.StopPhase(nameof(ModulePhase.PostConfigureServices));
        }
    }

    private void ReachCompositionWorkCheckpoint(ModuleCompositionWorkDeadline deadline)
    {
        var scheduler = _compositionWork
            ?? throw new InvalidOperationException("Module composition work has already completed.");
        if (scheduler.ReachCheckpoint(deadline).HasFailures)
        {
            throw new ModuleCompositionWorkFailureException(deadline);
        }
    }

    private ModuleCompositionWorkSnapshot FinalizeCompositionWork(bool abort)
    {
        if (_compositionWorkRecorded)
        {
            return new ModuleCompositionWorkSnapshot([], []);
        }

        var scheduler = _compositionWork
            ?? throw new InvalidOperationException("Module composition work scheduler is unavailable.");
        if (abort)
        {
            scheduler.AbortAndDrain();
        }

        var snapshot = scheduler.GetSnapshot();
        application.Profiling.RecordCompositionWork(snapshot);
        foreach (var failure in snapshot.WorkItems.Where(static result => !result.IsSucceeded))
        {
            application.Errors.RecordCompositionWorkError(failure);
        }

        _compositionWorkRecorded = true;
        scheduler.Dispose();
        _compositionWork = null;
        return snapshot;
    }

    private void HandleRegistrationFailure(Exception primaryFailure)
    {
        ModuleCompositionWorkSnapshot snapshot;
        try
        {
            snapshot = _compositionWorkRecorded || _compositionWork is null
                ? new ModuleCompositionWorkSnapshot([], [])
                : FinalizeCompositionWork(abort: true);
        }
        catch (Exception drainFailure)
        {
            application.Profiling.StopModuleSystem();
            throw new AggregateException(
                "Monica composition failed and scheduled composition work could not be drained cleanly.",
                primaryFailure,
                drainFailure);
        }

        application.Profiling.StopModuleSystem();
        var workFailures = snapshot.WorkItems
            .Where(static result => !result.IsSucceeded)
            .Select(static result => result.Failure!)
            .ToArray();
        if (workFailures.Length == 0)
        {
            return;
        }

        if (primaryFailure is ModuleCompositionWorkFailureException or ModuleRegistrationException)
        {
            application.Errors.RaiseModuleErrors();
        }

        throw new AggregateException(
            "Monica composition and required scheduled composition work both failed.",
            [primaryFailure, .. workFailures]);
    }

    /// <summary>
    /// Claims the application builder before Monica configures the Web pipeline.
    /// </summary>
    internal void BeginApplicationPipeline(IApplicationBuilder app)
    {
        _composition.BeginApplicationPipeline(app);
    }

    /// <summary>
    /// Claims the application builder before Monica maps Web endpoints.
    /// </summary>
    internal void BeginEndpointMapping(IApplicationBuilder app)
    {
        _composition.BeginEndpointMapping(app);
    }

    /// <summary>
    /// Completes composition once at the host-appropriate boundary.
    /// </summary>
    internal void CompleteComposition(ModuleCompositionCompletionPoint completionPoint)
    {
        if (!_composition.TryBeginCompletion(completionPoint))
        {
            return;
        }

        try
        {
            application.Profiling.StopModuleSystem();

            if (application.ModuleSystem.EnableSummaryLog)
            {
                Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}",
                    application.Profiling.GetPerformanceSummary());
                Logger.LogInformation("Module system register order summary:\n{Order}",
                    application.Dependencies.GetModuleRegistrationSummary());
            }

            application.Errors.RaiseModuleErrors();
            _composition.CommitCompletion();
        }
        catch (Exception exception)
        {
            _composition.FailCompletion(exception);
            throw;
        }
    }

    /// <summary>
    /// Gets a host-start failure when the Web pipeline has not completed Monica composition.
    /// </summary>
    internal string? GetStartupValidationFailure()
    {
        return _composition.GetStartupValidationFailure();
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
        CompleteComposition(ModuleCompositionCompletionPoint.EndpointMapping);
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
