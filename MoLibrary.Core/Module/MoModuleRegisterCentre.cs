using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module;

/// <summary>
/// 模块注册中心
/// </summary>
public static class MoModuleRegisterCentre
{
    /// <summary>
    /// 模块注册请求信息字典
    /// </summary>
    private static Dictionary<Type, ModuleRequestInfo> ModuleRegisterContextDict { get; set; } = new();

    public static ModuleRequestInfo RegisterModule<TModule>() where TModule : MoModule
    {
        var moduleType = typeof(TModule);
        if (ModuleRegisterContextDict.TryGetValue(moduleType, out var requestInfo)) return requestInfo;

        requestInfo = new ModuleRequestInfo();
        ModuleRegisterContextDict[moduleType] = requestInfo;
        return requestInfo;
    }
    public static ModuleRequestInfo BindModuleOption<TModule, TOption>() where TModule : MoModule where TOption : class, IMoModuleOption<TModule>, new()
    {
        var info = RegisterModule<TModule>();
        info.BindModuleOption<TOption>();
        return info;
    }
    public static void CreateRegisterRequest<TModule>(ModuleRegisterRequest request) where TModule : MoModule
    {
        var actions = RegisterModule<TModule>().RegisterRequests;
        actions.Add(request);
    }

 
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
}

public class ModuleRequestInfo
{
    public List<ModuleRegisterRequest> RegisterRequests { get; set; } = [];
    private Dictionary<Type, SortedList<int, Action<object>>> PendingConfigActions { get; } = [];
    public Dictionary<Type, object> FinalConfigures { get; set; } = [];

    /// <summary>
    /// 模块相关设置类型
    /// </summary>
    public Type ModuleOptionType { get; set; } = null!;
    
    /// <summary>
    /// 初始化最终配置，根据排序后的配置项获得最终配置对象，最后清空配置操作
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

    public void BindModuleOption<TOption>() where TOption : class, IMoModuleOption, new()
    {
        ModuleOptionType = typeof(TOption);
    }

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