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
    public static List<ModuleRegisterError> ModuleRegisterErrors { get; } = [];

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
    /// 模块快照列表，用于存储所有模块的快照信息。
    /// </summary>
    public static List<ModuleSnapshot> ModuleSnapshots { get; } = [];

    /// <summary>
    /// 模块注册请求信息字典，用于存储所有注册过的模块类型及其注册信息。
    /// </summary>
    public static Dictionary<Type, ModuleRequestInfo> ModuleRegisterContextDict { get; } = [];

    /// <summary>
    /// Attempts to retrieve the ModuleRequestInfo for a specified module type.
    /// </summary>
    /// <param name="type">The type of the module to retrieve information for.</param>
    /// <param name="requestInfo"></param>
    /// <returns>The ModuleRequestInfo if found; otherwise, null.</returns>
    public static bool TryGetModuleRequestInfo(Type type, [NotNullWhen(true)]out ModuleRequestInfo? requestInfo)
    {
        return ModuleRegisterContextDict.TryGetValue(type, out requestInfo);
    }

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
    /// 注册当前注册的所有模块的服务。此方法应在builder.Build()之前调用。
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例。</param>
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    internal static void RegisterServices(WebApplicationBuilder builder)
    {
        var services = builder.Services;
        

        // 清空之前的错误记录
        ModuleRegisterErrors.Clear();

        var typeFinder = services.GetOrCreateDomainTypeFinder<MoDomainTypeFinder>();
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.ClaimDependencies));
        // 1. 初次遍历所有注册的模块，判断若模块有依赖项，处理依赖关系
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => !p.Value.HasBeenBuilt).OrderBy(p => p.Value.Order).Select(p => p).ToList())
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
                ModuleErrorUtil.RecordModuleError(moduleType, ex.Message,
                    EMoModuleConfigMethods.ClaimDependencies, ModuleRegisterErrorType.InitializationError);
            }
        }

        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.ClaimDependencies));

        // 1.1 在所有依赖关系建立完成后，刷新模块注册顺序
        ModuleAnalyser.RefreshAllModuleOrders();

        var snapshots = new List<ModuleSnapshot>();

        // 2. 初始化模块配置
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.InitFinalConfigures));
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => !p.Value.HasBeenBuilt).OrderBy(p => p.Value.Order))
        {
            try
            {
                // 初始化模块配置
                info.InitFinalConfigures();
            }
            catch(Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(moduleType, ex.Message,
                    EMoModuleConfigMethods.InitFinalConfigures, ModuleRegisterErrorType.InitializationError);
            }
        }
        ModuleManager.Init();
        // 2.1 检查模块是否满足必要配置要求
        ModuleErrorUtil.ValidateModuleRequirements(ModuleRegisterContextDict.Where(p => !p.Value.HasBeenBuilt).ToDictionary());
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.InitFinalConfigures));

        // 2.2 注册模块服务
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.ConfigureBuilder)+nameof(EMoModuleConfigMethods.ConfigureServices));
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Where(p => !p.Value.HasBeenBuilt && !ModuleManager.IsModuleDisabled(p.Key)).OrderBy(p => p.Value.Order))
        {
            try
            {
                // 创建模块实例
                if (Activator.CreateInstance(moduleType, info.ModuleOption) is not MoModule module) continue;

                // 调用模块构建方法
                ModuleProfiler.StartModulePhase(moduleType, EMoModuleConfigMethods.ConfigureBuilder);
                var builderResult = module.ConfigureBuilder(builder);

                if (!builderResult.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(moduleType, builderResult.Message,
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
                        ModuleErrorUtil.RecordRequestError(moduleType, request, ex);
                    }
                }

                ModuleProfiler.StopModulePhase(moduleType, EMoModuleConfigMethods.ConfigureBuilder);


                // 调用模块注册方法
                ModuleProfiler.StartModulePhase(moduleType, EMoModuleConfigMethods.ConfigureServices);
                var servicesResult = module.ConfigureServices(services);

                if (!servicesResult.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(moduleType, servicesResult.Message,
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
                        ModuleErrorUtil.RecordRequestError(moduleType, request, ex);
                    }
                }

                ModuleProfiler.StopModulePhase(moduleType, EMoModuleConfigMethods.ConfigureServices);
                snapshots.Add(new ModuleSnapshot(module, info));
                info.HasBeenBuilt = true;
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(moduleType, ex.Message,
                    EMoModuleConfigMethods.ConfigureBuilder, ModuleRegisterErrorType.InitializationError);
            }
        }
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.ConfigureBuilder)+nameof(EMoModuleConfigMethods.ConfigureServices));


        // 3. 为需要遍历业务类型的模块提供支持
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.IterateBusinessTypes));
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
                    ModuleErrorUtil.RecordModuleError(module.ModuleInstance.GetType(), ex.Message,
                        EMoModuleConfigMethods.IterateBusinessTypes, ModuleRegisterErrorType.ConfigurationError);
                }
            }
        }

        if (needToIterate)
        {
            _ = businessTypes.ToList();
        }
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.IterateBusinessTypes));


        // 4. 执行模块的PostConfigureServices方法
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.PostConfigureServices));
        foreach (var module in snapshots)
        {
            try
            {
                ModuleProfiler.StartModulePhase(module.ModuleType, EMoModuleConfigMethods.PostConfigureServices);
                var postConfigResult = module.ModuleInstance.PostConfigureServices(services);

                if (!postConfigResult.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(module.ModuleType, postConfigResult.Message,
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
                        ModuleErrorUtil.RecordRequestError(module.ModuleType, request, ex);
                    }
                }

                ModuleProfiler.StopModulePhase(module.ModuleType, EMoModuleConfigMethods.PostConfigureServices);
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(module.ModuleType, ex.Message,
                    EMoModuleConfigMethods.PostConfigureServices, ModuleRegisterErrorType.ConfigurationError);
            }
        }
        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.PostConfigureServices));
        ModuleSnapshots.AddRange(snapshots);
        ModuleErrorUtil.RaiseModuleErrors();
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
                if(!afterGivenOrder)
                {
                    ModuleProfiler.StartModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureApplicationBuilder);
                    var result = module.ModuleInstance.ConfigureApplicationBuilder(app);

                    if (!result.IsOk())
                    {
                        ModuleErrorUtil.RecordModuleError(module.ModuleType, result.Message,
                            EMoModuleConfigMethods.ConfigureApplicationBuilder, ModuleRegisterErrorType.ConfigurationError);
                    }
                }

                foreach (var request in module.RequestInfo.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureApplicationBuilder).Where(filter).OrderBy(r => r.Order))
                {
                    try
                    {
                        request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
                    }
                    catch (Exception ex)
                    {
                        ModuleErrorUtil.RecordRequestError(module.ModuleType, request, ex);
                    }
                }

                ModuleProfiler.StopModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureApplicationBuilder);
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(module.ModuleType, ex.Message,
                    EMoModuleConfigMethods.ConfigureApplicationBuilder, ModuleRegisterErrorType.ConfigurationError);
            }
        }

        ModuleProfiler.StopPhase(phaseName);
    }

    /// <summary>
    /// 配置端点路由构建器
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    internal static void ConfigEndpoints(IApplicationBuilder app)
    {
        ModuleProfiler.StartPhase(nameof(EMoModuleConfigMethods.ConfigureEndpoints));

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
               
                if (!result.IsOk())
                {
                    ModuleErrorUtil.RecordModuleError(module.ModuleType, result.Message,
                        EMoModuleConfigMethods.ConfigureEndpoints, ModuleRegisterErrorType.ConfigurationError);
                }
                foreach (var request in module.RequestInfo.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureEndpoints).OrderBy(r => r.Order))
                {
                    try
                    {
                        request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
                    }
                    catch (Exception ex)
                    {
                        ModuleErrorUtil.RecordRequestError(module.ModuleType, request, ex);
                    }
                }

                ModuleProfiler.StopModulePhase(module.ModuleType, EMoModuleConfigMethods.ConfigureEndpoints);
            }
            catch (Exception ex)
            {
                ModuleErrorUtil.RecordModuleError(module.ModuleType, ex.Message,
                    EMoModuleConfigMethods.ConfigureEndpoints, ModuleRegisterErrorType.ConfigurationError);
            }

           
        }

        ModuleProfiler.StopPhase(nameof(EMoModuleConfigMethods.ConfigureEndpoints));
        ModuleProfiler.StopModuleSystem();

        // Log performance summary details
        Logger.LogInformation("Module system performance summary:\n{PerformanceSummary}",
            ModuleProfiler.GetPerformanceSummary());
        Logger.LogInformation("Module system register order summary:\n{Order}",
            ModuleAnalyser.GetModuleRegistrationSummary());

        ModuleErrorUtil.RaiseModuleErrors();
    }
}