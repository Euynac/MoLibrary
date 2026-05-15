using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Monica.Core;
using Monica.Core.Extensions;
using Monica.Core.Logging;
using Monica.Core.Modularity.Abstractions;
using Monica.Core.Modularity.Annotations;
using Monica.Core.Modularity.Exceptions;
using Monica.Core.Modularity.Models;
using Monica.Core.Modularity.Models.Internal;
using Monica.Core.Modularity.Services.Support;
using Monica.Tool.Extensions;

namespace Monica.Core.Modularity.Services;

/// <summary>
/// Central registry that drives the module registration lifecycle and manages module configuration and initialization.
/// </summary>
public static class ModuleRegistry
{
    /// <summary>
    /// Module registration errors.
    /// </summary>
    public static List<ModuleRegistrationError> ModuleRegisterErrors => MonicaApplication.Current.Registry.ModuleRegisterErrors;

    public static ILogger Logger { get; set; } = LogManager.For(typeof(ModuleRegistry));

    /// <summary>
    /// Module snapshots captured after successful registration.
    /// </summary>
    public static List<ModuleRuntimeSnapshot> ModuleSnapshots => MonicaApplication.Current.Registry.ModuleSnapshots;

    /// <summary>
    /// Registration information for every module type that has been registered.
    /// </summary>
    public static Dictionary<Type, ModuleRegistrationState> ModuleRegisterContextDict => MonicaApplication.Current.Registry.ModuleRegisterContextDict;

    /// <summary>
    /// Attempts to retrieve the ModuleRequestInfo for a specified module type.
    /// </summary>
    /// <param name="type">The type of the module to retrieve information for.</param>
    /// <param name="requestInfo"></param>
    /// <returns>The ModuleRequestInfo if found; otherwise, null.</returns>
    public static bool TryGetModuleRequestInfo(Type type, [NotNullWhen(true)] out ModuleRegistrationState? requestInfo)
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
    public static void AddModuleRegisterContext(Type moduleType, ModuleRegistrationState registerInfo)
    {
        if (!ModuleRegisterContextDict.TryAdd(moduleType, registerInfo))
        {
            throw new ModuleRegistrationException($"模块类型 {moduleType.FullName} 已存在");
        }

        // Start overall profiling when the first module is registered.
        if (ModuleRegisterContextDict.Count == 1)
        {
            ModuleInitializationProfiler.StartModuleSystem();
            Logger.LogInformation("Module system initialization started");
        }
    }

    /// <summary>
    /// Registers services for all currently registered modules.
    /// This method must run before `builder.Build()`.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    internal static void RegisterServices(IHostApplicationBuilder builder)
    {
        var services = builder.Services;


        // Clear any error state from a previous registration run.
        ModuleRegisterErrors.Clear();
        
        ModuleInitializationProfiler.StartPhase(nameof(ModulePhase.ClaimDependencies));
        // 1. First pass: let modules declare dependencies.

        while (ModuleRegisterContextDict.Where(p=>p.Value.ModulePhase == ModulePhase.None).ToList() is {Count: > 0} list)
        {
            foreach (var (moduleType, info) in list.OrderBy(p => p.Value.Order).Select(p => p).ToList())
            {
                info.StartModulePhase(ModulePhase.ClaimDependencies);
                try
                {
                    var option = info.CreateCurrentModuleOption();
                    if (Activator.CreateInstance(moduleType, option) is IModuleDependencyDeclarer moduleTmpInstance)
                    {
                        moduleTmpInstance.ClaimDependencies();
                    }
                }
                catch (Exception ex)
                {
                    ModuleErrorRegistry.RecordModuleError(moduleType, ex,
                        ModulePhase.ClaimDependencies, ModuleRegistrationErrorType.InitializationError);
                }
                finally
                {
                    info.EndModulePhase(ModulePhase.ClaimDependencies);
                }
            }
        }
       

        ModuleInitializationProfiler.StopPhase(nameof(ModulePhase.ClaimDependencies));

        // 1.1 Refresh module ordering after all dependencies have been declared.
        ModuleDependencyAnalyzer.RefreshAllModuleOrders();

        var snapshots = new List<ModuleRuntimeSnapshot>();

        // 2. Materialize the final configuration objects for each module.
        ModuleInitializationProfiler.StartPhase(nameof(ModulePhase.InitFinalConfigures));
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => p.Value.ModulePhase == ModulePhase.ClaimDependencies).OrderBy(p => p.Value.Order))
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
                    $"{moduleType.GetCleanFullName()}进行{nameof(ModuleRegistrationState.InitFinalConfigures)}时出现异常");
            }
        }
        ModuleStateRegistry.Init();
        ValidateWebModuleCompatibility(builder);
        // 2.1 Validate required configuration for every initialized module.
        ModuleErrorRegistry.ValidateModuleRequirements(ModuleRegisterContextDict.Where(p => p.Value.ModulePhase == ModulePhase.InitFinalConfigures).ToDictionary());
        ModuleInitializationProfiler.StopPhase(nameof(ModulePhase.InitFinalConfigures));

        // 2.2 Execute builder and service registrations.
        ModuleInitializationProfiler.StartPhase(nameof(ModulePhase.ConfigureBuilder) + nameof(ModulePhase.ConfigureServices));
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => p.Value.ModulePhase == ModulePhase.InitFinalConfigures).OrderBy(p => p.Value.Order))
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
                    ModuleErrorRegistry.RecordRequestError(moduleType, request, ex);
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
                    ModuleErrorRegistry.RecordRequestError(moduleType, request, ex);
                }
            }

            info.EndModulePhase(ModulePhase.ConfigureServices);
            snapshots.Add(new ModuleRuntimeSnapshot(info.ModuleSingleton!, info));
        }
        ModuleInitializationProfiler.StopPhase(nameof(ModulePhase.ConfigureBuilder) + nameof(ModulePhase.ConfigureServices));


        // 3. Allow modules to inspect and transform discovered business types.
        ModuleInitializationProfiler.StartPhase(nameof(ModulePhase.IterateBusinessTypes));
        var businessTypes = Mo.TypeFinder.GetTypes()
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
                throw ex.CreateException(Logger, "模块迭代出现异常");
            }
        }
        ModuleInitializationProfiler.StopPhase(nameof(ModulePhase.IterateBusinessTypes));


        // 4. Execute post-service configuration hooks.
        ModuleInitializationProfiler.StartPhase(nameof(ModulePhase.PostConfigureServices));
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
                    ModuleErrorRegistry.RecordRequestError(module.ModuleType, request, ex);
                }
            }

            module.RegisterInfo.EndModulePhase(ModulePhase.PostConfigureServices);
        }
        ModuleInitializationProfiler.StopPhase(nameof(ModulePhase.PostConfigureServices));
        ModuleSnapshots.AddRange(snapshots);
        ModuleErrorRegistry.RaiseModuleErrors();
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
        ModuleInitializationProfiler.StartPhase(phaseName);

        Func<ModuleConfigurationRequest, bool> filter = afterGivenOrder ? request => request.Order > order : request => request.Order <= order;
        // Execute application builder requests in priority order.
        foreach (var module in ModuleSnapshots.Where(p =>
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
                    ModuleErrorRegistry.RecordRequestError(module.ModuleType, request, ex);
                }
            }
            
            module.RegisterInfo.EndModulePhase(ModulePhase.ConfigureApplicationBuilder);

        }

        ModuleInitializationProfiler.StopPhase(phaseName);
    }

    /// <summary>
    /// Configures endpoints for the registered modules.
    /// </summary>
    /// <param name="app">The application builder.</param>
    internal static void ConfigEndpoints(IApplicationBuilder app)
    {
        ModuleInitializationProfiler.StartPhase(nameof(ModulePhase.ConfigureEndpoints));

        // Execute endpoint configuration requests in priority order.
        foreach (var module in ModuleSnapshots.Where(p =>
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
                    ModuleErrorRegistry.RecordRequestError(module.ModuleType, request, ex);
                }
            }

            module.RegisterInfo.EndModulePhase(ModulePhase.ConfigureEndpoints);
        }

        ModuleInitializationProfiler.StopPhase(nameof(ModulePhase.ConfigureEndpoints));
        ModuleInitializationProfiler.StopModuleSystem();

        if (Mo.ModuleSystem.EnableSummaryLog)
        {
            // Log performance summary details
            Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}",
                ModuleInitializationProfiler.GetPerformanceSummary());
            Logger.LogInformation("Module system register order summary:\n{Order}",
                ModuleDependencyAnalyzer.GetModuleRegistrationSummary());
        }
       

        ModuleErrorRegistry.RaiseModuleErrors();
    }

    /// <summary>
    /// Gets all module snapshots that are providers for a specific target module.
    /// </summary>
    /// <param name="targetModuleKey">The ModuleKey of the target module to find providers for</param>
    /// <returns>List of ModuleSnapshots for modules that provide for the target module</returns>
    public static List<ModuleRuntimeSnapshot> GetModuleProviders(ModuleKey targetModuleKey)
    {
        return ModuleSnapshots
            .Where(s => s.ModuleInstance is IModuleProvider provider
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
        where TProvider : IModuleProvider
    {
        return ModuleSnapshots
            .Where(s => s.ModuleInstance is TProvider provider
                        && provider.ProvidesFor == targetModuleKey)
            .Select(s => (TProvider)s.ModuleInstance)
            .ToList();
    }

    private static void ValidateWebModuleCompatibility(IHostApplicationBuilder builder)
    {
        if (builder is WebApplicationBuilder)
        {
            return;
        }

        foreach (var (moduleType, info) in ModuleRegisterContextDict
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

            var moduleKey = ModuleDependencyAnalyzer.ResolveModuleKey(moduleType);
            ModuleErrorRegistry.RecordHostCompatibilityError(
                moduleType,
                $"Module {moduleType.Name} ({moduleKey}) requires an ASP.NET Core host and cannot downgrade to a non-web module.");
        }

        ModuleErrorRegistry.RaiseModuleErrors();
    }
}
