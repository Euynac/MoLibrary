using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

/// <summary>
/// 模块注册上下文，用于处理模块注册过程中的服务配置
/// </summary>
/// <param name="services">服务集合</param>
/// <param name="moduleRequestInfo">模块请求信息</param>
public class ModuleRegisterContext(IServiceCollection services, ModuleRequestInfo moduleRequestInfo)
{
    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection Services { get; init; } = services;
    
    /// <summary>
    /// 模块请求信息
    /// </summary>
    public ModuleRequestInfo ModuleRequestInfo { get; init; } = moduleRequestInfo;

    /// <summary>
    /// 当前模块相关模块设置
    /// </summary>
    internal Dictionary<Type, object> Option  => ModuleRequestInfo.FinalConfigures;
}

/// <summary>
/// 泛型模块注册上下文，提供特定模块选项的访问
/// </summary>
/// <typeparam name="TModuleOption">模块选项类型</typeparam>
/// <param name="services">服务集合</param>
/// <param name="moduleRequestInfo">模块请求信息</param>
public class ModuleRegisterContext<TModuleOption>(IServiceCollection services, ModuleRequestInfo moduleRequestInfo) 
    : ModuleRegisterContext(services, moduleRequestInfo) where TModuleOption : IMoModuleOption
{
    /// <summary>
    /// 获取当前模块的设置
    /// </summary>
    public TModuleOption ModuleOption => (TModuleOption) Option[typeof(TModuleOption)];
    
    /// <summary>
    /// 获取模块额外选项，如果不存在则创建新实例
    /// </summary>
    /// <typeparam name="TModuleExtraOption">模块额外选项类型</typeparam>
    /// <returns>模块额外选项实例</returns>
    public TModuleExtraOption GetModuleExtraOption<TModuleExtraOption>() where TModuleExtraOption : IMoModuleOption, new()
    {
        return GetModuleExtraOptionOrDefault<TModuleExtraOption>() ?? new TModuleExtraOption();
    }
    
    /// <summary>
    /// 获取模块额外选项，如果不存在则返回默认值
    /// </summary>
    /// <typeparam name="TModuleExtraOption">模块额外选项类型</typeparam>
    /// <returns>模块额外选项实例或默认值</returns>
    public TModuleExtraOption? GetModuleExtraOptionOrDefault<TModuleExtraOption>() where TModuleExtraOption : IMoModuleOption, new()
    {
        if (Option.TryGetValue(typeof(TModuleExtraOption), out var option))
        {
            return (TModuleExtraOption) option;
        }

        return default;
    }
}

public class ModuleRegisterRequest(string key)
{
    public Action<ModuleRegisterContext>? ConfigureContext { get; set; }
    /// <summary>
    /// 相同Key的配置只执行一次
    /// </summary>
    public string Key { get; set; } = key;
    public EMoModules? RequestFrom { get; set; }
    public int Order { get; set; }

    public void SetConfigureContext<TModuleOption>(Action<ModuleRegisterContext<TModuleOption>> context) where TModuleOption : IMoModuleOption
    {
        ConfigureContext = registerContext =>
        {
            context.Invoke((ModuleRegisterContext<TModuleOption>) registerContext);
        };
    }
}