using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using MoLibrary.Tool.Extensions;
using MoLibrary.Core.Module.TypeFinder;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.BuilderWrapper;
using Microsoft.Extensions.Logging;
using MoLibrary.Core.Features.MoLogProvider;
using MoLibrary.Core.Module.Exceptions;
using MoLibrary.Tool.MoResponse;
using MoLibrary.Core.Module.Features;

namespace MoLibrary.Core.Module;

/// <summary>
/// 模块注册中心，用于控制模块注册生命周期并管理所有模块的配置和初始化。
/// </summary>
public static class MoModuleRegisterCentre
{
    /// <summary>
    /// 模块注册错误列表
    /// </summary>
    private static List<ModuleRegisterError> ModuleRegisterErrors { get; } = [];

    public static ILogger Logger { get; set; } = LogProvider.For(typeof(MoModuleRegisterCentre));
    /// <summary>
    /// 静态构造函数，用于初始化事件监听。
    /// </summary>
    static MoModuleRegisterCentre()
    {
        // 注册BeforeBuild事件处理程序，用于在构建应用程序前注册服务
        WebApplicationBuilderExtensions.BeforeBuild += RegisterServices;

        // 注册BeforeUseRouting事件处理程序，用于在路由中间件应用前执行操作
        WebApplicationBuilderExtensions.BeforeUseRouting += app => ConfigApplicationPipeline(app, ModuleOrder.MiddlewareUseRouting, false);

        // 注册AfterUseRouting事件处理程序，用于在路由中间件应用后执行操作
        WebApplicationBuilderExtensions.AfterUseRouting += app => ConfigApplicationPipeline(app, ModuleOrder.MiddlewareUseRouting, true);

        // 注册BeforeUseEndpoints事件处理程序，用于在Endpoints配置前执行操作
        WebApplicationBuilderExtensions.BeginUseEndpoints += ConfigEndpoints;
    }

    /// <summary>
    /// 模块注册请求信息字典，用于存储所有注册过的模块类型及其注册信息。
    /// </summary>
    public static Dictionary<Type, ModuleRequestInfo> ModuleRegisterContextDict { get; } = new();

    /// <summary>
    /// 添加模块注册上下文
    /// </summary>
    /// <param name="moduleType">模块类型</param>
    /// <param name="requestInfo">模块请求信息</param>
    public static void AddModuleRegisterContext(Type moduleType, ModuleRequestInfo requestInfo)
    {
        if (!ModuleRegisterContextDict.TryAdd(moduleType, requestInfo))
        {
            throw new ModuleRegisterException($"模块类型 {moduleType.FullName} 已存在");
        }

        // 如果是第一次添加模块，开始计时
        if (ModuleRegisterContextDict.Count == 1)
        {
            ModuleProfiler.StartModuleSystem();
            Logger.LogInformation("Module system initialization started");
        }
    }

    /// <summary>
    /// 模块快照列表，用于存储所有模块的快照信息。
    /// </summary>
    private static List<ModuleSnapshot> ModuleSnapshots { get; } = [];

    /// <summary>
    /// 注册当前注册的所有模块的服务。此方法应在builder.Build()之前调用。
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例。</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    internal static void RegisterServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        ModuleProfiler.StartPhase(nameof(RegisterServices));

        // 清空之前的错误记录
        ModuleRegisterErrors.Clear();

        var typeFinder = services.GetOrCreateDomainTypeFinder<MoDomainTypeFinder>();

        // 1. 初次遍历所有注册的模块，判断若模块有依赖项，处理依赖关系
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p=>!p.Value.HasBeenBuilt && !ModuleManager.IsModuleDisabled(p.Key)).Select(p=>p).ToList())
        {
            if (!moduleType.IsImplementInterface(typeof(IWantDependsOnOtherModules))) continue;

            try
            {
                //根据info.ModuleOptionType创建模块配置实例
                var option = Activator.CreateInstance(info.ModuleOptionType);
                if (Activator.CreateInstance(moduleType, option) is IWantDependsOnOtherModules moduleTmpInstance)
                {
                    moduleTmpInstance.ClaimDependencies();
                }
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, moduleType, ex.Message, 
                    EMoModuleConfigMethods.ClaimDependencies, ModuleRegisterErrorType.InitializationError);
            }
        }
        
        //// 1.1 Check for circular dependencies in the dependency graph
        //if (MoModuleAnalyser.HasCircularDependencies())
        //{
        //    var error = new ModuleRegisterError
        //    {
        //        ErrorMessage = "Circular dependency detected in module dependencies. Please check the dependency graph.",
        //        ErrorType = ModuleRegisterErrorType.CircularDependency
        //    };
        //    ModuleRegisterErrors.Add(error);
            
        //    // Log the dependency graph for debugging
        //    var graph = MoModuleAnalyser.CalculateCompleteModuleDependencyGraph();
            
        //   Logger.LogError("Module dependency graph contains circular dependencies:\n{Graph}", graph.ToString());
             
        //    // Continue with registration but warn about potential issues
        //}
        //else
        //{
        //    var modulesInOrder = MoModuleAnalyser.GetModulesInDependencyOrder();

        //    var graph = MoModuleAnalyser.CalculateCompleteModuleDependencyGraph();
        //    Logger.LogInformation("Modules will be initialized in the following dependency order: {Modules}\n{Graph}",
        //        string.Join(", ", modulesInOrder), graph);
        //}
        
        // 1.3 检查模块是否满足必要配置要求
        ModuleErrorUtil.ValidateModuleRequirements(ModuleRegisterContextDict.Where(p => !p.Value.HasBeenBuilt && !ModuleManager.IsModuleDisabled(p.Key)).ToDictionary(), ModuleRegisterErrors);

        var snapshots = new List<ModuleSnapshot>();

        // 2. 初始化模块配置并注册服务
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => !p.Value.HasBeenBuilt && !ModuleManager.IsModuleDisabled(p.Key)))
        {
            try
            {
                // 初始化模块配置
                info.InitFinalConfigures();

                // 创建模块实例
                if (Activator.CreateInstance(moduleType, info.ModuleOption) is not MoModule module) continue;

                // 调用模块构建方法
                ModuleProfiler.StartModulePhase(moduleType, EMoModuleConfigMethods.ConfigureBuilder);
                var builderResult = module.ConfigureBuilder(builder);
                ModuleProfiler.StopModulePhase(moduleType, EMoModuleConfigMethods.ConfigureBuilder);

                if (!builderResult.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, moduleType, builderResult.Message, 
                        EMoModuleConfigMethods.ConfigureBuilder, ModuleRegisterErrorType.ConfigurationError);
                }

                // 执行额外的配置请求
                foreach (var request in info.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureBuilder).OrderBy(r => r.Order))
                {
                    try
                    {
                        request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, info));
                    }
                    catch (Exception ex)
                    {
                        ModuleErrorUtil.RecordRequestError(ModuleRegisterErrors, moduleType, request, ex);
                    }
                }

                // 调用模块注册方法
                ModuleProfiler.StartModulePhase(moduleType, EMoModuleConfigMethods.ConfigureServices);
                var servicesResult = module.ConfigureServices(services);
                ModuleProfiler.StopModulePhase(moduleType, EMoModuleConfigMethods.ConfigureServices);

                if (!servicesResult.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, moduleType, servicesResult.Message, 
                        EMoModuleConfigMethods.ConfigureServices, ModuleRegisterErrorType.ConfigurationError);
                }

                // 执行额外的配置请求
                foreach (var request in info.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureServices).OrderBy(r => r.Order))
                {
                    try
                    {
                        request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, info));
                    }
                    catch (Exception ex)
                    {
                        ModuleErrorUtil.RecordRequestError(ModuleRegisterErrors, moduleType, request, ex);
                    }
                }

                snapshots.Add(new ModuleSnapshot(module, info));
                info.HasBeenBuilt = true;
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, moduleType, ex.Message,
                    EMoModuleConfigMethods.ConfigureBuilder, ModuleRegisterErrorType.InitializationError);
            }
        }
        
        
        // 3. 为需要遍历业务类型的模块提供支持
        ModuleProfiler.StartPhase(nameof(IWantIterateBusinessTypes.IterateBusinessTypes));
        var businessTypes = typeFinder.GetTypes();
        var needToIterate = false;
        foreach (var module in snapshots)
        {
            if (module.ModuleInstance is IWantIterateBusinessTypes iterateModule)
            {
                needToIterate = true;
                try
                {
                    businessTypes = iterateModule.IterateBusinessTypes(businessTypes);
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleInstance.GetType(), ex.Message,
                        EMoModuleConfigMethods.IterateBusinessTypes, ModuleRegisterErrorType.ConfigurationError);
                }
            }
        }

        if (needToIterate)
        {
            _ = businessTypes.ToList();
        }
        ModuleProfiler.StopPhase(nameof(IWantIterateBusinessTypes.IterateBusinessTypes));


        // 4. 执行模块的PostConfigureServices方法
        foreach (var module in snapshots)
        {
            try
            {
                ModuleProfiler.StartModulePhase(module.ModuleType, EMoModuleConfigMethods.PostConfigureServices);
                var postConfigResult = module.ModuleInstance.PostConfigureServices(services);
                ModuleProfiler.StopModulePhase(module.ModuleType, EMoModuleConfigMethods.PostConfigureServices);

                if (!postConfigResult.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleType, postConfigResult.Message, 
                        EMoModuleConfigMethods.PostConfigureServices, ModuleRegisterErrorType.ConfigurationError);
                }

                // 执行额外的配置请求
                foreach (var request in module.RequestInfo.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.PostConfigureServices).OrderBy(r => r.Order))
                {
                    try
                    {
                        request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, module.RequestInfo));
                    }
                    catch (Exception ex)
                    {
                        ModuleErrorUtil.RecordRequestError(ModuleRegisterErrors, module.ModuleType, request, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleType, ex.Message,
                    EMoModuleConfigMethods.PostConfigureServices, ModuleRegisterErrorType.ConfigurationError);
            }
        }
        ModuleSnapshots.AddRange(snapshots);

        var elapsedTime = ModuleProfiler.StopPhase(nameof(RegisterServices));
        Logger.LogInformation("Module services registration completed in {ElapsedMilliseconds}ms. Total module system time: {TotalElapsedMilliseconds}ms", 
            elapsedTime, ModuleProfiler.GetTotalElapsedMilliseconds());
        ModuleErrorUtil.RaiseModuleErrors(ModuleRegisterErrors);
    }

    /// <summary>
    /// 配置应用程序管道
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    /// <param name="order">配置顺序。</param>
    /// <param name="afterGivenOrder">是否在给定顺序之后配置。</param>
    internal static void ConfigApplicationPipeline(IApplicationBuilder app, int order, bool afterGivenOrder)
    {
        var phaseName = afterGivenOrder ? $"{nameof(ConfigApplicationPipeline)}_After_{order}" : $"{nameof(ConfigApplicationPipeline)}_Before_{order}";
        ModuleProfiler.StartPhase(phaseName);
        
        Func<ModuleRegisterRequest, bool> filter = afterGivenOrder ? request => request.Order > order : request => request.Order <= order;
        // 按优先级排序并配置应用程序构建器
        foreach (var module in ModuleSnapshots)
        {
            // Skip disabled modules
            if (ModuleManager.IsModuleDisabled(module.ModuleType))
            {
                continue;
            }
            
            try
            {
                ModuleProfiler.StartModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureApplicationBuilder);
                var result = module.ModuleInstance.ConfigureApplicationBuilder(app);
                ModuleProfiler.StopModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureApplicationBuilder);

                if (!result.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleType, result.Message, 
                        EMoModuleConfigMethods.ConfigureApplicationBuilder, ModuleRegisterErrorType.ConfigurationError);
                }
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleType, ex.Message,
                    EMoModuleConfigMethods.ConfigureApplicationBuilder, ModuleRegisterErrorType.ConfigurationError);
            }

            foreach (var request in module.RequestInfo.RegisterRequests.Where(p=>p.RequestMethod == EMoModuleConfigMethods.ConfigureApplicationBuilder).Where(filter).OrderBy(r => r.Order))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(ModuleRegisterErrors, module.ModuleType, request, ex);
                }
            }
        }
        
        var elapsedTime = ModuleProfiler.StopPhase(phaseName);
        Logger.LogInformation("Application pipeline configuration {PhaseType} completed in {ElapsedMilliseconds}ms. Total module system time: {TotalElapsedMilliseconds}ms", 
            afterGivenOrder ? "after routing" : "before routing", 
            elapsedTime, ModuleProfiler.GetTotalElapsedMilliseconds());
    }

    /// <summary>
    /// 配置端点路由构建器
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    internal static void ConfigEndpoints(IApplicationBuilder app)
    {
        ModuleProfiler.StartPhase(nameof(ConfigEndpoints));
        
        // 按优先级排序并配置端点路由构建器
        foreach (var module in ModuleSnapshots)
        {
            // Skip disabled modules
            if (ModuleManager.IsModuleDisabled(module.ModuleType))
            {
                continue;
            }
            
            try
            {
                ModuleProfiler.StartModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureEndpoints);
                var result = module.ModuleInstance.ConfigureEndpoints(app);
                ModuleProfiler.StopModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureEndpoints);

                if (!result.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleType, result.Message, 
                        EMoModuleConfigMethods.ConfigureEndpoints, ModuleRegisterErrorType.ConfigurationError);
                }
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(ModuleRegisterErrors, module.ModuleType, ex.Message,
                    EMoModuleConfigMethods.ConfigureEndpoints, ModuleRegisterErrorType.ConfigurationError);
            }

            foreach (var request in module.RequestInfo.RegisterRequests.Where(p=>p.RequestMethod == EMoModuleConfigMethods.ConfigureEndpoints).OrderBy(r => r.Order))
            {
                try
                {
                    request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
                }
                catch (Exception ex)
                {
                    ModuleErrorUtil.RecordRequestError(ModuleRegisterErrors, module.ModuleType, request, ex);
                }
            }
        }
        
        var elapsedTime = ModuleProfiler.StopPhase(nameof(ConfigEndpoints));
        ModuleProfiler.StopModuleSystem();
        
        // Log final performance summary
        Logger.LogInformation("Endpoints configuration completed in {ElapsedMilliseconds}ms. Total module system initialization time: {TotalElapsedMilliseconds}ms", 
            elapsedTime, ModuleProfiler.GetTotalElapsedMilliseconds());
        
        // Log performance summary details
        Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}", 
            ModuleProfiler.GetPerformanceSummary());
        
        ModuleErrorUtil.RaiseModuleErrors(ModuleRegisterErrors);
    }
}