using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Tool.Extensions;
using MoLibrary.Core.Module.TypeFinder;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.BuilderWrapper;
using System.Text;
using MoLibrary.Core.Module.Exceptions;

namespace MoLibrary.Core.Module;

/// <summary>
/// 模块注册中心，用于控制模块注册生命周期并管理所有模块的配置和初始化。
/// </summary>
public static class MoModuleRegisterCentre
{

    /// <summary>
    /// Dictionary mapping module types to their enum representations.
    /// </summary>
    public static Dictionary<Type, EMoModules> ModuleTypeToEnumMap { get; set; } = new();
    /// <summary>
    /// 模块注册错误列表
    /// </summary>
    private static List<ModuleRegisterError> ModuleRegisterErrors { get; } = [];

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
    /// 模块快照列表，用于存储所有模块的快照信息。
    /// </summary>
    private static List<ModuleSnapshot> ModuleSnapshots { get; } = [];

    
    /// <summary>
    /// 注册所有模块的服务。此方法应在builder.Build()之前调用。
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例。</param>
    /// <param name="typeFinderConfigure">类型查找器的配置操作。</param>
    internal static void RegisterServices(WebApplicationBuilder builder, Action<ModuleCoreOptionTypeFinder>? typeFinderConfigure = null)
    {
        var services = builder.Services;

        // 清空之前的错误记录
        ModuleRegisterErrors.Clear();

        var typeFinder = services.GetOrCreateDomainTypeFinder<MoDomainTypeFinder>(typeFinderConfigure);

        // 1. 初次遍历所有注册的模块，判断若模块有依赖项，处理依赖关系
        foreach (var (moduleType, info) in ModuleRegisterContextDict.Select(p=>p).ToList())
        {
            if (!moduleType.IsImplementInterface(typeof(IWantDependsOnOtherModules))) continue;

            //根据info.ModuleOptionType创建模块配置实例
            var option = Activator.CreateInstance(info.ModuleOptionType);
            if (Activator.CreateInstance(moduleType, option) is IWantDependsOnOtherModules moduleTmpInstance)
            {
                moduleTmpInstance.ClaimDependencies();
            }
        }
        
        // 1.1 检查模块是否满足必要配置要求
        ModuleErrorUtil.ValidateModuleRequirements(ModuleRegisterContextDict, ModuleRegisterErrors);

        // 2. 初始化模块配置并注册服务
        foreach (var (moduleType, info) in ModuleRegisterContextDict)
        {
            // 初始化模块配置
            info.InitFinalConfigures();

            // 创建模块实例
            if (Activator.CreateInstance(moduleType, info.FinalConfigures[info.ModuleOptionType]) is not MoModule module) continue;

            // 调用模块构建方法
            module.ConfigureBuilder(builder);
            // 执行额外的配置请求
            foreach (var request in info.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureBuilder).OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, info));
            }

            // 调用模块注册方法
            module.ConfigureServices(services);
            // 执行额外的配置请求
            foreach (var request in info.RegisterRequests.Where(p => p.RequestMethod == EMoModuleConfigMethods.ConfigureServices).OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, info));
            }

            ModuleSnapshots.Add(new ModuleSnapshot(module, info));
        }

        var businessTypes = typeFinder.GetTypes();
        
        // 3. 为需要遍历业务类型的模块提供支持
        foreach (var module in ModuleSnapshots)
        {
            if (module.ModuleInstance is IWantIterateBusinessTypes iterateModule)
            {
                businessTypes = iterateModule.IterateBusinessTypes(businessTypes);
            }
        }

        _ = businessTypes.ToList();


        // 4. 执行模块的PostConfigureServices方法
        foreach (var module in ModuleSnapshots)
        {
            module.ModuleInstance.PostConfigureServices(services);

            // 执行额外的配置请求
            foreach (var request in module.RequestInfo.RegisterRequests.Where(p=>p.RequestMethod == EMoModuleConfigMethods.PostConfigureServices).OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, null, builder, module.RequestInfo));
            }
        }
        // 清理临时资源
        ModuleRegisterContextDict.Clear();
    }

    /// <summary>
    /// 配置应用程序管道
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    /// <param name="order">配置顺序。</param>
    /// <param name="afterGivenOrder">是否在给定顺序之后配置。</param>
    internal static void ConfigApplicationPipeline(IApplicationBuilder app, int order, bool afterGivenOrder)
    {
        Func<ModuleRegisterRequest, bool> filter = afterGivenOrder ? request => request.Order > order : request => request.Order <= order;
        // 按优先级排序并配置应用程序构建器
        foreach (var module in ModuleSnapshots)
        {
            module.ModuleInstance.ConfigureApplicationBuilder(app);
            foreach (var request in module.RequestInfo.RegisterRequests.Where(p=>p.RequestMethod == EMoModuleConfigMethods.ConfigureApplicationBuilder).Where(filter).OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
            }
        }
    }

    /// <summary>
    /// 配置端点路由构建器
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    internal static void ConfigEndpoints(IApplicationBuilder app)
    {
        // 按优先级排序并配置端点路由构建器
        foreach (var module in ModuleSnapshots)
        {
            module.ModuleInstance.ConfigureEndpoints(app);
            foreach (var request in module.RequestInfo.RegisterRequests.Where(p=>p.RequestMethod == EMoModuleConfigMethods.ConfigureEndpoints).OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
            }
        }
    }
}