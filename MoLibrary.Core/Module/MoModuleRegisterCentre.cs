using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module;

/// <summary>
/// 模块注册中心，用于控制模块注册生命周期并管理所有模块的配置和初始化。
/// </summary>
public static class MoModuleRegisterCentre
{
    /// <summary>
    /// 模块注册请求信息字典，用于存储所有注册过的模块类型及其注册信息。
    /// </summary>
    private static Dictionary<Type, ModuleRequestInfo> ModuleRegisterContextDict { get; set; } = new();

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
                    context.Services.Configure(optionAction);
                },
                Order = guideFrom != EMoModules.Developer ? order - 1 : order, //来自模块级联注册的Option的优先级始终比用户Order低1
                RequestFrom = guideFrom
            });
    }

    /// <summary>
    /// 注册所有模块的服务。此方法应在builder.Build()之前调用。
    /// </summary>
    /// <param name="services">服务集合。</param>
    public static void MoModuleRegisterServices(this IServiceCollection services)
    {
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

            // 为需要遍历业务类型的模块提供支持
            if (module is IWantIterateBusinessTypes iterateModule)
            {
                // 此处应该有遍历业务类型的代码
                // ...
            }

            module.PostConfigureServices(services);

            // 执行额外的配置请求
            foreach (var request in info.RegisterRequests.OrderBy(r => r.Order))
            {
                request.ConfigureContext?.Invoke(new ModuleRegisterContext(services, info));
            }
        }

        // 清理临时资源
        ModuleRegisterContextDict.Clear();
    }

    /// <summary>
    /// 配置所有模块的中间件。此方法应在app.Build()之后、任何中间件配置之前调用。
    /// </summary>
    /// <param name="app">应用程序构建器。</param>
    public static void MoModuleUseMiddlewares(this IApplicationBuilder app)
    {
        // 获取注册的所有模块实例
        var moduleServices = app.ApplicationServices.GetServices<MoModule>().ToList();

        // 按优先级排序并配置应用程序构建器
        foreach (var module in moduleServices)
        {
            module.ConfigureApplicationBuilder(app);
        }
    }
}

/// <summary>
/// 模块注册上下文，用于传递给模块注册请求。
/// </summary>
public class MoModuleContext
{
    /// <summary>
    /// 服务集合。
    /// </summary>
    public IServiceCollection Services { get; set; } = null!;
}

/// <summary>
/// 模块请求信息，用于存储模块的注册请求和配置信息。
/// </summary>
public class ModuleRequestInfo
{
    /// <summary>
    /// 模块的注册请求列表。
    /// </summary>
    public List<ModuleRegisterRequest> RegisterRequests { get; set; } = [];

    /// <summary>
    /// 待处理的配置操作字典，按配置类型和执行顺序排序。
    /// </summary>
    private Dictionary<Type, SortedList<int, Action<object>>> PendingConfigActions { get; } = [];

    /// <summary>
    /// 最终配置对象字典，按配置类型索引。
    /// </summary>
    public Dictionary<Type, object> FinalConfigures { get; set; } = [];

    /// <summary>
    /// 模块相关设置类型。
    /// </summary>
    public Type ModuleOptionType { get; set; } = null!;
    
    /// <summary>
    /// 初始化最终配置，根据排序后的配置项获得最终配置对象，最后清空配置操作。
    /// </summary>
    public void InitFinalConfigures()
    {
        foreach (var configType in PendingConfigActions.Keys)
        {
            // 创建配置类型的实例
            var configInstance = Activator.CreateInstance(configType);
            
            if (configInstance == null)
                continue;
            
            // 获取该类型的所有配置操作（已按优先级排序）
            var sortedActions = PendingConfigActions[configType];
            
            // 按顺序应用所有配置操作到实例上
            foreach (var action in sortedActions.Values)
            {
                action.Invoke(configInstance);
            }
            
            // 将最终配置保存到字典中
            FinalConfigures[configType] = configInstance;
        }
        
        // 清空待处理的配置操作
        PendingConfigActions.Clear();
    }

    /// <summary>
    /// 绑定模块选项类型。
    /// </summary>
    /// <typeparam name="TOption">模块选项类型。</typeparam>
    public void BindModuleOption<TOption>() where TOption : class, IMoModuleOption, new()
    {
        ModuleOptionType = typeof(TOption);
    }

    /// <summary>
    /// 添加配置操作到待处理队列。
    /// </summary>
    /// <typeparam name="TOption">模块选项类型。</typeparam>
    /// <param name="order">配置操作执行顺序。</param>
    /// <param name="optionAction">配置操作委托。</param>
    public void AddConfigureAction<TOption>(int order, Action<TOption> optionAction) where TOption : class, IMoModuleOption, new()
    {
        var type = typeof(TOption);
        if (!PendingConfigActions.TryGetValue(type, out var actions))
        {
            actions = new SortedList<int, Action<object>>();
            PendingConfigActions[type] = actions;
        }
        actions.Add(order, p =>
        {
            optionAction.Invoke((TOption) p);
        });
    }
}