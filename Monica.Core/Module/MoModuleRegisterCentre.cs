using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Monica.Core.Extensions;
using Monica.Core.Features.MoLogProvider;
using Monica.Core.Module.BuilderWrapper;
using Monica.Core.Module.Exceptions;
using Monica.Core.Module.Features;
using Monica.Core.Module.Interfaces;
using Monica.Core.Module.Models;
using Monica.Tool.Extensions;

namespace Monica.Core.Module;

/// <summary>
/// Central registry that drives the module registration lifecycle and manages module configuration and initialization.
/// </summary>
public static class MoModuleRegisterCentre
{
    /// <summary>
    /// Module registration errors.
    /// </summary>
    public static List<ModuleRegisterError> ModuleRegisterErrors { get; } = [];

    public static ILogger Logger { get; set; } = LogProvider.For(typeof(MoModuleRegisterCentre));
    /// <summary>
    /// Static constructor that wires the module lifecycle events.
    /// </summary>
    static MoModuleRegisterCentre()
    {
        // Register services before the application is built.
        WebApplicationBuilderExtensions.BeforeBuild += RegisterServices;

        // Configure middleware that should run before `UseRouting`.
        WebApplicationBuilderExtensions.BeforeUseRouting += app => ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, false);

        // Configure middleware that should run after `UseRouting`.
        WebApplicationBuilderExtensions.AfterUseRouting += app => ConfigApplicationPipeline(app, ModuleOrder.MIDDLEWARE_USE_ROUTING, true);

        // Configure endpoints before the application reaches endpoint mapping.
        WebApplicationBuilderExtensions.BeginUseEndpoints += ConfigEndpoints;
    }

    /// <summary>
    /// Module snapshots captured after successful registration.
    /// </summary>
    public static List<ModuleSnapshot> ModuleSnapshots { get; } = [];

    /// <summary>
    /// Registration information for every module type that has been registered.
    /// </summary>
    public static Dictionary<Type, ModuleRegisterInfo> ModuleRegisterContextDict { get; } = [];

    /// <summary>
    /// Attempts to retrieve the ModuleRequestInfo for a specified module type.
    /// </summary>
    /// <param name="type">The type of the module to retrieve information for.</param>
    /// <param name="requestInfo"></param>
    /// <returns>The ModuleRequestInfo if found; otherwise, null.</returns>
    public static bool TryGetModuleRequestInfo(Type type, [NotNullWhen(true)] out ModuleRegisterInfo? requestInfo)
    {
        return ModuleRegisterContextDict.TryGetValue(type, out requestInfo);
    }

    /// <summary>
    /// Gets all keyed service keys registered by the specified module.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <returns>The keyed service keys for the module, or an empty set if the module is unknown.</returns>
    public static IReadOnlySet<string> GetKeyedServiceKeys(Type moduleType)
    {
        return TryGetModuleRequestInfo(moduleType, out var info)
            ? info.KeyedServiceKeys
            : new HashSet<string>();
    }

    /// <summary>
    /// Adds module registration information for a module type.
    /// </summary>
    /// <param name="moduleType">The module type.</param>
    /// <param name="registerInfo">The registration information.</param>
    public static void AddModuleRegisterContext(Type moduleType, ModuleRegisterInfo registerInfo)
    {
        if (!ModuleRegisterContextDict.TryAdd(moduleType, registerInfo))
        {
            throw new ModuleRegisterException($"模块类型 {moduleType.FullName} 已存在");
        }

        // Start overall profiling when the first module is registered.
        if (ModuleRegisterContextDict.Count == 1)
        {
            ModuleProfiler.StartModuleSystem();
            Logger.LogInformation("Module system initialization started");
        }
    }

    /// <summary>
    /// Registers services for all currently registered modules.
    /// This method must run before `builder.Build()`.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    internal static void RegisterServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;


        // Clear any error state from a previous registration run.
        ModuleRegisterErrors.Clear();
        
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.ClaimDependencies));
        // 1. First pass: let modules declare dependencies.

        while (ModuleRegisterContextDict.Where(p=>p.Value.ModulePhase == EMoModuleConfigMethods.None).ToList() is {Count: > 0} list)
        {
            foreach (var (moduleType, info) in list.OrderBy(p => p.Value.Order).Select(p => p).ToList())
            {
                info.StartModulePhase(EMoModuleConfigMethods.ClaimDependencies);
                try
                {
                    var option = info.CreateCurrentModuleOption();
                    if (Activator.CreateInstance(moduleType, option) is IDependsOnOtherModules moduleTmpInstance)
                    {
                        moduleTmpInstance.ClaimDependencies();
                    }
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordModuleError(moduleType, ex,
                        EMoModuleConfigMethods.ClaimDependencies, ModuleRegisterErrorType.InitializationError);
                }
                finally
                {
                    info.EndModulePhase(EMoModuleConfigMethods.ClaimDependencies);
                }
            }
        }
       

        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.ClaimDependencies));

        // 1.1 Refresh module ordering after all dependencies have been declared.
        ModuleAnalyser.RefreshAllModuleOrders();

        var snapshots = new List<ModuleSnapshot>();

        // 2. Materialize the final configuration objects for each module.
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.InitFinalConfigures));
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => p.Value.ModulePhase == EMoModuleConfigMethods.ClaimDependencies).OrderBy(p => p.Value.Order))
        {
            try
            {
                info.StartModulePhase(EMoModuleConfigMethods.InitFinalConfigures);
                // Finalize the module configuration objects.
                info.InitFinalConfigures();
                info.EndModulePhase(EMoModuleConfigMethods.InitFinalConfigures);
            }
            catch (Exception ex)
            {
                throw ex.CreateException(Logger,
                    $"{moduleType.GetCleanFullName()}进行{nameof(ModuleRegisterInfo.InitFinalConfigures)}时出现异常");
            }
        }
        ModuleManager.Init();
        // 2.1 Validate required configuration for every initialized module.
        ModuleErrorUtil.ValidateModuleRequirements(ModuleRegisterContextDict.Where(p => p.Value.ModulePhase == EMoModuleConfigMethods.InitFinalConfigures).ToDictionary());
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.InitFinalConfigures));

        // 2.2 Execute builder and service registrations.
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.ConfigureBuilder) + nameof(EMoModuleConfigMethods.ConfigureServices));
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => p.Value.ModulePhase == EMoModuleConfigMethods.InitFinalConfigures).OrderBy(p => p.Value.Order))
        {
            // Run builder configuration first.
            info.StartModulePhase(EMoModuleConfigMethods.ConfigureBuilder);

            // Execute queued builder configuration requests.
            foreach (var request in info.DeduplicateRequests(
                info.RegisterRequests
                    .Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureBuilder)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, info));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(moduleType, request, ex);
                }
            }

            info.EndModulePhase(EMoModuleConfigMethods.ConfigureBuilder);


            // Then run service registrations.
            info.StartModulePhase(EMoModuleConfigMethods.ConfigureServices);

            // Execute queued service configuration requests.
            foreach (var request in info.DeduplicateRequests(
                info.RegisterRequests
                    .Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureServices)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, info));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(moduleType, request, ex);
                }
            }

            info.EndModulePhase(EMoModuleConfigMethods.ConfigureServices);
            snapshots.Add(new ModuleSnapshot(info.ModuleSingleton!, info));
        }
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.ConfigureBuilder) + nameof(EMoModuleConfigMethods.ConfigureServices));


        // 3. Allow modules to inspect and transform discovered business types.
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.IterateBusinessTypes));
        var businessTypes = Mo.Options.GlobalTypeFinder.GetTypes();
        var needToIterate = false;
        foreach (var module in snapshots.Where(p => p.RegisterInfo.ModulePhase == EMoModuleConfigMethods.ConfigureServices))
        {
            if (module.ModuleInstance is not IWantIterateBusinessTypes iterateModule) continue;
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
                throw ex.CreateException(Logger, "模块迭代出现异常");
            }
        }
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.IterateBusinessTypes));


        // 4. Execute post-service configuration hooks.
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.PostConfigureServices));
        foreach (var module in snapshots)
        {
            module.RegisterInfo.StartModulePhase(EMoModuleConfigMethods.PostConfigureServices);
            // Execute queued post-configuration requests.
            foreach (var request in module.RegisterInfo.DeduplicateRequests(
                module.RegisterInfo.RegisterRequests
                    .Where(p => p.RequestMethod == EMoModuleConfigMethods.PostConfigureServices)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, module.RegisterInfo));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(module.ModuleType, request, ex);
                }
            }

            module.RegisterInfo.EndModulePhase(EMoModuleConfigMethods.PostConfigureServices);
        }
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.PostConfigureServices));
        ModuleSnapshots.AddRange(snapshots);
        ModuleErrorUtil.RaiseModuleErrors();
    }

    /// <summary>
    /// Configures the application pipeline for the registered modules.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="order">The ordering split point.</param>
    /// <param name="afterGivenOrder">Whether to configure items after the given order instead of before it.</param>
    internal static void ConfigApplicationPipeline(IApplicationBuilder app, int order, bool afterGivenOrder)
    {
        var phaseName = afterGivenOrder ? $"{nameof(ConfigApplicationPipeline)}_After_{order}" : $"{nameof(ConfigApplicationPipeline)}_Before_{order}";
        ModuleProfiler.StartPhase(phaseName);

        Func<ModuleRegisterRequest, bool> filter = afterGivenOrder ? request => request.Order > order : request => request.Order <= order;
        // Execute application builder requests in priority order.
        foreach (var module in ModuleSnapshots.Where(p => p.RegisterInfo.ModulePhase is EMoModuleConfigMethods.PostConfigureServices or EMoModuleConfigMethods.ConfigureApplicationBuilder))
        {
            module.RegisterInfo.StartModulePhase(EMoModuleConfigMethods.ConfigureApplicationBuilder);

            foreach (var request in module.RegisterInfo.DeduplicateRequests(
                module.RegisterInfo.RegisterRequests
                    .Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureApplicationBuilder)
                    .Where(filter)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RegisterInfo));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(module.ModuleType, request, ex);
                }
            }
            
            module.RegisterInfo.EndModulePhase(EMoModuleConfigMethods.ConfigureApplicationBuilder);

        }

        ModuleProfiler.StopPhase(phaseName);
    }

    /// <summary>
    /// Configures endpoints for the registered modules.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal static void ConfigEndpoints(IApplicationBuilder app)
    {
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.ConfigureEndpoints));

        // Execute endpoint configuration requests in priority order.
        foreach (var module in ModuleSnapshots.Where(p => p.RegisterInfo.ModulePhase == EMoModuleConfigMethods.ConfigureApplicationBuilder))
        {
            module.RegisterInfo.StartModulePhase(EMoModuleConfigMethods.ConfigureEndpoints);

            foreach (var request in module.RegisterInfo.DeduplicateRequests(
                module.RegisterInfo.RegisterRequests
                    .Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureEndpoints)
                    .OrderBy(r => r.Order)))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RegisterInfo));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(module.ModuleType, request, ex);
                }
            }

            module.RegisterInfo.EndModulePhase(EMoModuleConfigMethods.ConfigureEndpoints);
        }

        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.ConfigureEndpoints));
        ModuleProfiler.StopModuleSystem();

        if (Mo.Options.EnableLoggingModuleSummary)
        {
            // Log performance summary details
            Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}",
                ModuleProfiler.GetPerformanceSummary());
            Logger.LogInformation("Module system register order summary:\n{Order}",
                ModuleAnalyser.GetModuleRegistrationSummary());
        }
       

        ModuleErrorUtil.RaiseModuleErrors();
    }

    /// <summary>
    /// Gets all module snapshots that are providers for a specific target module.
    /// </summary>
    /// <param name="targetModuleKey">The ModuleKey of the target module to find providers for</param>
    /// <returns>List of ModuleSnapshots for modules that provide for the target module</returns>
    public static List<ModuleSnapshot> GetModuleProviders(ModuleKey targetModuleKey)
    {
        return ModuleSnapshots
            .Where(s => s.ModuleInstance is IMoModuleProvider provider
                        && provider.ProvidesFor == targetModuleKey)
            .ToList();
    }

    /// <summary>
    /// Gets all module providers of a specific type for a target module.
    /// </summary>
    /// <typeparam name="TProvider">The provider interface type</typeparam>
    /// <param name="targetModuleKey">The ModuleKey of the target module to find providers for</param>
    /// <returns>List of provider instances</returns>
    public static List<TProvider> GetModuleProviders<TProvider>(ModuleKey targetModuleKey)
        where TProvider : IMoModuleProvider
    {
        return ModuleSnapshots
            .Where(s => s.ModuleInstance is TProvider provider
                        && provider.ProvidesFor == targetModuleKey)
            .Select(s => (TProvider)s.ModuleInstance)
            .ToList();
    }
}
