using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 模块请求信息，用于存储模块的注册请求和配置信息。
/// </summary>
public class ModuleRequestInfo
{
    /// <summary>
    /// 模块是否已经被注册构建，如一些可提前注册的模块，在正式构建时需跳过。
    /// </summary>
    public bool HasBeenBuilt { get; set; }
    
    /// <summary>
    /// 模块注册顺序，数值越小越优先注册。用于控制模块按依赖关系的注册顺序。
    /// </summary>
    public int Order { get; set; } = 1000; // 默认值为1000，确保有依赖的模块可以设置更小的值
    
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
    /// 模块的选项实例，可以用于访问模块的配置信息。
    /// 在初始化配置后可用。
    /// </summary>
    public IMoModuleOption ModuleOption => (IMoModuleOption)FinalConfigures[ModuleOptionType];
    
    /// <summary>
    /// 必须配置的方法键列表，若未配置则会抛出异常。
    /// </summary>
    public List<string> RequiredConfigMethodKeys { get; set; } = [];

    /// <summary>
    /// 模块单例，初始化模块配置阶段设置
    /// </summary>
    public MoModule? ModuleSingleton { get; internal set; }
    
    /// <summary>
    /// 初始化最终配置，根据排序后的配置项获得最终配置对象，最后清空配置操作。
    /// </summary>
    public void InitFinalConfigures(Type moduleType)
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

        if(!FinalConfigures.ContainsKey(ModuleOptionType))
        {
            FinalConfigures[ModuleOptionType] = Activator.CreateInstance(ModuleOptionType)!;
        }

        if (Activator.CreateInstance(moduleType, ModuleOption) is MoModule instance)
        {
            instance.ConvertToRegisterRequest();
            ModuleSingleton = instance;
        }

        if (ModuleSingleton == null)
        {
            throw new Exception($"{moduleType.GetCleanFullName()}模块初始化最终设置失败！未能生成模块单例");
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
    /// <param name="guideFrom"></param>
    /// <param name="caller"></param>
    public void AddConfigureAction<TOption>(int order, Action<TOption> optionAction, EMoModules? guideFrom,
        string caller) where TOption : class, IMoModuleOptionBase, new()
    {
        RegisterRequests.Add(
            new ModuleRegisterRequest($"{caller}:ConfigOption<{typeof(TOption).Name}>_{Guid.NewGuid()}")
            {
                ConfigureContext = context =>
                {
                    context.Services!.Configure(optionAction);
                },
                RequestMethod = EMoModuleConfigMethods.ConfigureServices,
                Order = guideFrom != EMoModules.Developer ? order - 1 : order, //来自模块级联注册的Option的优先级始终比用户Order低1
                RequestFrom = guideFrom
            });

        var type = typeof(TOption);
        if (!PendingConfigActions.TryGetValue(type, out var actions))
        {
            actions = new SortedList<int, Action<object>>(new DuplicateKeyComparer<int>());
            PendingConfigActions[type] = actions;
        }
        actions.Add(order, p =>
        {
            optionAction.Invoke((TOption) p);
        });
    }

    /// <summary>
    /// 检查是否所有必须配置的方法键都已配置
    /// </summary>
    /// <returns>未配置的方法键列表，如果全部已配置则返回空列表</returns>
    public List<string> GetMissingRequiredConfigMethodKeys()
    {
        if (RequiredConfigMethodKeys.Count == 0)
            return [];
        
        var configuredKeys = RegisterRequests.Select(r => r.Key).ToHashSet();
        return RequiredConfigMethodKeys
            .Where(key => !configuredKeys.Contains(key))
            .ToList();
    }
}