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
using Monica.Core.Modularity.Diagnostics.Models;
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
    private const string CONFIGURE_BUILDER_AND_SERVICES_PHASE =
        nameof(ModulePhase.ConfigureBuilder) + " / " + nameof(ModulePhase.ConfigureServices);

    private readonly ModuleCompositionState _composition = new();
    private readonly object _compositionCallbackGate = new();
    private readonly ModuleRegistryState _state = new();
    private ModuleRegistrationState? _activeCompositionCallback;
    private int _activeCompositionCallbackThreadId;
    private ModuleStartupWorkScheduler? _startupWork;
    private TaskCompletionSource<string?>? _startupValidation;
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
        _startupWork = new ModuleStartupWorkScheduler(
            application.ModuleSystem.MaxConcurrentStartupWorkItems,
            OnStartupWorkCompleted);
        application.Profiling.AttachStartupWorkDiagnostics(_startupWork.GetSnapshot);
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
            ReachStartupWorkBarrier(ModuleStartupWorkBarrier.BeforeBusinessTypeIteration);

            IterateBusinessTypes(snapshots);
            ReachStartupWorkBarrier(ModuleStartupWorkBarrier.BeforePostConfigureServices);

            ExecutePostConfigureServices(builder, services, snapshots);
            _startupWork.CloseSubmissions();
            ReachStartupWorkBarrier(ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion);

            _state.AddRuntimeSnapshots(snapshots);
            application.Errors.RaiseModuleErrors();
            application.Profiling.RecordMilestone(ModuleCompositionMilestone.ServiceRegistrationCompleted);
            if (builder is not WebApplicationBuilder)
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
        // Generic Host resolves HostOptions while it builds IHost. Make the host-owned Monica application an options
        // dependency so the container materializes and owns it even when the host is disposed without being started.
        services.AddOptions<HostOptions>()
            .Configure<MonicaApplication>(static (_, _) => { });
        services.AddSingleton<IMonicaApplicationOptions>(application.Application);
        services.AddSingleton<IMonicaModuleSystemOptions>(application.ModuleSystem);
        RegisterStartupValidation(services);

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
        application.Profiling.StartPhase(CONFIGURE_BUILDER_AND_SERVICES_PHASE);
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
            application.Profiling.StopPhase(CONFIGURE_BUILDER_AND_SERVICES_PHASE);
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
            var iterators = snapshots
                .Where(static snapshot => snapshot.ModuleInstance is IBusinessTypeIterator)
                .ToArray();
            if (iterators.Length == 0)
            {
                return;
            }

            IReadOnlyList<Type> businessTypes = application.TypeFinder.GetTypes()
                .Where(static type => !type.IsDefined(
                    typeof(ExcludeFromBusinessTypeDiscoveryAttribute),
                    inherit: false))
                .ToArray();
            foreach (var snapshot in iterators)
            {
                var iterator = (IBusinessTypeIterator)snapshot.ModuleInstance;
                snapshot.RegisterInfo.StartModulePhase(ModulePhase.IterateBusinessTypes);
                try
                {
                    BeginCompositionCallback(snapshot.RegisterInfo);
                    try
                    {
                        // Keep the callback active through enumeration because iterator bodies execute lazily.
                        businessTypes = iterator.IterateBusinessTypes(businessTypes).ToArray();
                    }
                    finally
                    {
                        EndCompositionCallback(snapshot.RegisterInfo);
                    }
                }
                finally
                {
                    snapshot.RegisterInfo.EndModulePhase(ModulePhase.IterateBusinessTypes);
                }
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
        DisposeStartupWork();
        _startupValidation = null;
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
    internal void ScheduleStartupWork(
        ModuleBase owner,
        string name,
        Action work,
        Action? commit,
        ModuleStartupWorkBarrier barrier)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(work);

        ValidateSynchronousCompositionDelegate(
            work,
            nameof(work),
            "Module startup work must be synchronous. Use the Generic Host lifecycle for asynchronous work.");
        if (commit is not null)
        {
            ValidateSynchronousCompositionDelegate(
                commit,
                nameof(commit),
                "Module startup work commits must be synchronous and cannot launch asynchronous work.");
        }

        if (!Enum.IsDefined(barrier))
        {
            throw new ArgumentOutOfRangeException(nameof(barrier), barrier, "Unknown startup work barrier.");
        }

        if (commit is not null
            && barrier is ModuleStartupWorkBarrier.BeforeHostLifecycle or ModuleStartupWorkBarrier.NoBarrier)
        {
            throw new InvalidOperationException(
                $"Startup work targeting {barrier} cannot have a serial commit because service registration " +
                "is sealed before that barrier.");
        }

        var moduleType = owner.GetType();
        if (!TryGetModuleRequestInfo(moduleType, out var info)
            || !ReferenceEquals(info.ModuleSingleton, owner))
        {
            throw new InvalidOperationException(
                $"Only the materialized host-owned {moduleType.Name} instance can schedule startup work.");
        }

        lock (_compositionCallbackGate)
        {
            if (!ReferenceEquals(_activeCompositionCallback, info)
                || _activeCompositionCallbackThreadId != Environment.CurrentManagedThreadId
                || info.ModulePhase is not (ModulePhase.ConfigureBuilder
                    or ModulePhase.ConfigureServices
                    or ModulePhase.IterateBusinessTypes
                    or ModulePhase.PostConfigureServices))
            {
                throw new InvalidOperationException(
                    $"Module {moduleType.Name} can schedule startup work only while its synchronous " +
                    "ConfigureBuilder, ConfigureServices, IterateBusinessTypes, or PostConfigureServices " +
                    "callback is executing.");
            }

            if (info.ModulePhase == ModulePhase.IterateBusinessTypes
                && barrier == ModuleStartupWorkBarrier.BeforeBusinessTypeIteration)
            {
                throw new InvalidOperationException(
                    $"Module {moduleType.Name} cannot schedule startup work for " +
                    $"{ModuleStartupWorkBarrier.BeforeBusinessTypeIteration} during business-type iteration. " +
                    $"Use any later barrier: {ModuleStartupWorkBarrier.BeforePostConfigureServices}, " +
                    $"{ModuleStartupWorkBarrier.BeforeServiceRegistrationCompletion}, " +
                    $"{ModuleStartupWorkBarrier.BeforeHostLifecycle}, or {ModuleStartupWorkBarrier.NoBarrier}.");
            }
        }

        var scheduler = _startupWork
            ?? throw new InvalidOperationException("Module startup work is no longer available for this host.");
        scheduler.Schedule(
            moduleType,
            application.Dependencies.ResolveModuleKey(moduleType),
            info.Order,
            name,
            info.ModulePhase,
            barrier,
            work,
            commit);
    }

    private static void ValidateSynchronousCompositionDelegate(
        Action callback,
        string parameterName,
        string errorMessage)
    {
        if (callback.GetInvocationList().Any(static invocation =>
                invocation.Method.IsDefined(typeof(AsyncStateMachineAttribute), inherit: false)))
        {
            throw new ArgumentException(errorMessage, parameterName);
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

    private void RegisterStartupValidation(IServiceCollection services)
    {
        services.AddSingleton<IValidateOptions<ModuleStartupValidationOptions>>(
            new ModuleStartupValidator(application));
        services.AddOptions<ModuleStartupValidationOptions>().ValidateOnStart();
        services.AddHostedService<ModuleStartupWorkLifecycle>();
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

    private void ReachStartupWorkBarrier(ModuleStartupWorkBarrier barrier)
    {
        var scheduler = _startupWork
            ?? throw new InvalidOperationException("Module startup work is unavailable for this host.");
        var release = scheduler.ReachBarrier(barrier);
        if (release.HasFailures)
        {
            var failures = release.WorkItems.Where(static result => !result.IsSucceeded).ToArray();
            foreach (var failure in failures)
            {
                application.Errors.RecordStartupWorkError(failure);
            }

            throw new ModuleStartupWorkFailureException(barrier, failures);
        }

        CommitStartupWork(release.WorkItems);
    }

    private void CommitStartupWork(IReadOnlyList<ModuleStartupWorkResult> workItems)
    {
        foreach (var result in workItems)
        {
            if (result.Commit is not { } commit)
            {
                continue;
            }

            if (!TryGetModuleRequestInfo(result.ModuleType, out var registration))
            {
                throw new InvalidOperationException(
                    $"Startup work '{result.Name}' cannot commit because module {result.ModuleType.FullName} " +
                    "is no longer registered.");
            }

            registration.StartModulePhase(result.OriginPhase);
            try
            {
                // The worker barrier is already released. This serial publication is intentionally profiled as a
                // repeated callback of the phase that scheduled the work rather than as worker wait time.
                commit();
            }
            catch (Exception exception)
            {
                application.Errors.RecordStartupWorkCommitError(result, exception);
                application.Errors.RaiseModuleErrors();
                throw;
            }
            finally
            {
                registration.EndModulePhase(result.OriginPhase);
            }
        }
    }

    private void HandleRegistrationFailure(Exception primaryFailure)
    {
        var scheduler = _startupWork;
        ModuleStartupWorkSnapshot snapshot = new([], []);
        try
        {
            if (scheduler is not null)
            {
                scheduler.Drain();
                snapshot = scheduler.GetSnapshot();
            }
        }
        catch (Exception drainFailure)
        {
            application.Profiling.StopModuleSystem();
            throw new AggregateException(
                "Monica composition failed and scheduled startup work could not be drained cleanly.",
                primaryFailure,
                drainFailure);
        }

        application.Profiling.StopModuleSystem();
        var workFailures = snapshot.WorkItems
            .Where(static result => result.Barrier != ModuleStartupWorkBarrier.NoBarrier && !result.IsSucceeded)
            .Select(static result => result.Failure!)
            .ToArray();
        if (workFailures.Length == 0)
        {
            return;
        }

        if (primaryFailure is ModuleStartupWorkFailureException or ModuleRegistrationException)
        {
            application.Errors.RaiseModuleErrors();
        }

        throw new AggregateException(
            "Monica composition and required scheduled startup work both failed.",
            [primaryFailure, .. workFailures]);
    }

    /// <summary>
    /// Claims the application builder before Monica configures the Web pipeline.
    /// </summary>
    internal void BeginApplicationPipeline(IApplicationBuilder app)
    {
        _composition.BeginApplicationPipeline(app);
        application.Profiling.RecordMilestone(ModuleCompositionMilestone.ApplicationPipelineStarted);
    }

    /// <summary>
    /// Records successful completion of the <c>UseMonica()</c> application-pipeline boundary.
    /// </summary>
    internal void CompleteApplicationPipeline()
    {
        application.Profiling.RecordMilestone(ModuleCompositionMilestone.ApplicationPipelineCompleted);
    }

    /// <summary>
    /// Claims the application builder before Monica maps Web endpoints.
    /// </summary>
    internal void BeginEndpointMapping(IApplicationBuilder app)
    {
        _composition.BeginEndpointMapping(app);
        application.Profiling.RecordMilestone(ModuleCompositionMilestone.EndpointMappingStarted);
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
            application.Errors.RaiseModuleErrors();
            application.Profiling.RecordMilestone(ModuleCompositionMilestone.CompositionCompleted);
            application.Profiling.StopModuleSystem();

            if (application.ModuleSystem.EnableSummaryLog)
            {
                Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}",
                    application.Profiling.GetPerformanceSummary());
                Logger.LogInformation("Module system register order summary:\n{Order}",
                    application.Dependencies.GetModuleRegistrationSummary());
            }

            _composition.CommitCompletion();
        }
        catch (Exception exception)
        {
            application.Profiling.StopModuleSystem();
            _composition.FailCompletion(exception);
            throw;
        }
    }

    /// <summary>
    /// Gets a host-start failure when the Web pipeline has not completed Monica composition.
    /// </summary>
    internal string? GetStartupValidationFailure()
    {
        if (_composition.GetStartupValidationFailure() is { } compositionFailure)
        {
            return compositionFailure;
        }

        var candidate = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var validation = Interlocked.CompareExchange(ref _startupValidation, candidate, null);
        if (validation is not null)
        {
            return validation.Task.GetAwaiter().GetResult();
        }

        try
        {
            ReachStartupWorkBarrier(ModuleStartupWorkBarrier.BeforeHostLifecycle);
            candidate.TrySetResult(null);
        }
        catch (Exception exception)
        {
            candidate.TrySetResult(exception.GetMessageRecursively());
        }

        return candidate.Task.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Drains host-owned non-blocking work after the Generic Host has stopped.
    /// </summary>
    internal void DrainStartupWork()
    {
        _startupWork?.Drain();
    }

    /// <summary>
    /// Drains and releases the startup-work scheduler during application disposal.
    /// </summary>
    internal void DisposeStartupWork()
    {
        var scheduler = Interlocked.Exchange(ref _startupWork, null);
        scheduler?.Dispose();
    }

    private void OnStartupWorkCompleted(ModuleStartupWorkResult result)
    {
        if (result.Barrier == ModuleStartupWorkBarrier.NoBarrier && !result.IsSucceeded)
        {
            Logger.LogError(
                result.Failure,
                "Non-blocking startup work {WorkName} owned by module {ModuleType} failed. " +
                "Host startup is unaffected; inspect module-system diagnostics for details.",
                result.Name,
                result.ModuleType.FullName);
        }
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
