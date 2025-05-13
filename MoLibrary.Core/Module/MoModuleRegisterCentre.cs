using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Tool.Extensions;
using MoLibrary.Core.Module.TypeFinder;
using MoLibrary.Core.Module.Models;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Core.Module.BuilderWrapper;

namespace MoLibrary.Core.Module;

/// <summary>
/// 模块注册中心，用于控制模块注册生命周期并管理所有模块的配置和初始化。
/// </summary>
public static class MoModuleRegisterCentre
{
    /// <summary>
    /// 静态构造函数，用于初始化事件监听。
    /// </summary>
    static MoModuleRegisterCentre()
    {
        // 注册BeforeBuild事件处理程序，用于在构建应用程序前注册服务
        WebApplicationBuilderExtensions.BeforeBuild += MoModuleRegisterServices;
        
        // 注册AfterBuild事件处理程序，用于在构建应用程序后配置应用程序管道
        WebApplicationBuilderExtensions.AfterBuild += MoModuleConfigApplicationBuilder;
    }

    /// <summary>
    /// 模块注册请求信息字典，用于存储所有注册过的模块类型及其注册信息。
    /// </summary>
    private static Dictionary<Type, ModuleRequestInfo> ModuleRegisterContextDict { get; } = new();

    private static List<ModuleSnapshot> ModuleSnapshots { get; } = [];

    /// <summary>
    /// 注册模块类型并获取其注册请求信息。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <returns>模块的注册请求信息。</returns>
    private static ModuleRequestInfo RegisterModule<TModule>() where TModule : MoModule
    {
        var moduleType = typeof(TModule);
        if (ModuleRegisterContextDict.TryGetValue(moduleType, out var requestInfo)) return requestInfo;

        requestInfo = new ModuleRequestInfo();
        ModuleRegisterContextDict[moduleType] = requestInfo;
        return requestInfo;
    }

    /// <summary>
    /// 注册模块并绑定其配置选项。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <typeparam name="TOption">模块配置类型。</typeparam>
    /// <returns>模块的注册请求信息。</returns>
    public static ModuleRequestInfo RegisterModule<TModule, TOption>() where TModule : MoModule where TOption : class, IMoModuleOption<TModule>, new()
    {
        var info = RegisterModule<TModule>();
        info.BindModuleOption<TOption>();
        return info;
    }

    /// <summary>
    /// 注册模块并添加注册请求。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <typeparam name="TOption">模块配置类型。</typeparam>
    /// <param name="request">注册请求。</param>
    public static void RegisterModule<TModule, TOption>(ModuleRegisterRequest request) where TModule : MoModule<TModule, TOption> where TOption : class, IMoModuleOption<TModule>, new()
    {
        var actions = RegisterModule<TModule, TOption>().RegisterRequests;
        actions.Add(request);
    }

    /// <summary>
    /// 添加模块配置操作。
    /// </summary>
    /// <typeparam name="TModule">模块类型。</typeparam>
    /// <typeparam name="TOption">模块配置类型。</typeparam>
    /// <param name="order">配置操作执行顺序。</param>
    /// <param name="optionAction">配置操作委托。</param>
    /// <param name="guideFrom">配置操作来源模块。</param>
    public static void AddConfigureAction<TModule, TOption>(int order, Action<TOption> optionAction, EMoModules? guideFrom) where TModule : MoModule where TOption : class, IMoModuleOption, new()
    {
        var requestInfo = RegisterModule<TModule>();

        requestInfo.AddConfigureAction(order, optionAction);

        requestInfo.RegisterRequests.Add(
            new ModuleRegisterRequest($"ConfigureOption_{typeof(TOption).Name}_{Guid.NewGuid()}")
            {
                ConfigureContext = context =>
                {
                    context.Services!.Configure(optionAction);
                },
                RequestMethod = EMoModuleConfigMethods.ConfigureServices,
                Order = guideFrom != EMoModules.Developer ? order - 1 : order, //来自模块级联注册的Option的优先级始终比用户Order低1
                RequestFrom = guideFrom
            });
    }

    /// <summary>
    /// 注册所有模块的服务。此方法应在builder.Build()之前调用。
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="typeFinderConfigure"></param>
    public static void MoModuleRegisterServices(WebApplicationBuilder builder, Action<ModuleCoreOptionTypeFinder>? typeFinderConfigure = null)
    {
        var services = builder.Services;


        var typeFinder = services.GetOrCreateDomainTypeFinder<MoDomainTypeFinder>(typeFinderConfigure);

        // 1. 初次遍历所有注册的模块，判断若模块有依赖项，处理依赖关系
        foreach (var (moduleType, info) in ModuleRegisterContextDict)
        {
            if (!moduleType.IsImplementInterface(typeof(IWantDependsOnOtherModules))) continue;

            //根据info.ModuleOptionType创建模块配置实例
            var option = Activator.CreateInstance(info.ModuleOptionType);
            if (Activator.CreateInstance(moduleType, option) is IWantDependsOnOtherModules moduleTmpInstance)
            {
                moduleTmpInstance.ClaimDependencies();
            }
        }

        // 2. 初始化模块配置并注册服务
        foreach (var (moduleType, info) in ModuleRegisterContextDict)
        {
            // 初始化模块配置
            info.InitFinalConfigures();

            // 创建模块实例
            if (Activator.CreateInstance(moduleType, info.FinalConfigures[info.ModuleOptionType]) is not MoModule module) continue;

            // 调用模块注册方法
            module.ConfigureServices(services);

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
    /// 配置应用程序管道。此方法应在app.Build()之后、任何中间件配置之前调用。
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    public static void MoModuleConfigApplicationBuilder(IApplicationBuilder app)
    {
        // 按优先级排序并配置应用程序构建器
        foreach (var module in ModuleSnapshots)
        {
            module.ModuleInstance.ConfigureApplicationBuilder(app);
            foreach (var request in module.RequestInfo.RegisterRequests.Where(p=>p.RequestMethod == EMoModuleConfigMethods.ConfigureApplicationBuilder).OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(null, app, null, module.RequestInfo));
            }
        }
    }
}