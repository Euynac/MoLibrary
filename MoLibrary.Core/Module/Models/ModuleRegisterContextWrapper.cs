using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Core.Module.Models;

/// <summary>
/// 模块注册上下文，用于处理模块注册过程中的服务配置
/// </summary>
/// <param name="services">服务集合</param>
/// <param name="moduleRegisterInfo">模块请求信息</param>
public class ModuleRegisterContext(IServiceCollection? services, IApplicationBuilder? applicationBuilder, WebApplicationBuilder? webApplicationBuilder, ModuleRegisterInfo moduleRegisterInfo)
{
    /// <summary>
    /// 服务集合
    /// </summary>
    public IServiceCollection? Services { get; init; } = services;

    /// <summary>
    /// 应用构建器
    /// </summary>
    public IApplicationBuilder? ApplicationBuilder { get; init; } = applicationBuilder;

    /// <summary>
    /// 应用
    /// </summary>
    public WebApplication? WebApplication => ApplicationBuilder == null ? null : ApplicationBuilder as WebApplication ?? throw new InvalidOperationException($"当前{nameof(ApplicationBuilder)}是{ApplicationBuilder.GetType().GetCleanFullName()}类型，而不是{nameof(WebApplication)}类型！");
    
    /// <summary>
    /// 应用构建器
    /// </summary>
    public WebApplicationBuilder? WebApplicationBuilder { get; init; } = webApplicationBuilder;
    
    /// <summary>
    /// 模块请求信息
    /// </summary>
    public ModuleRegisterInfo ModuleRegisterInfo { get; init; } = moduleRegisterInfo;

    /// <summary>
    /// 当前模块相关模块设置
    /// </summary>
    internal Dictionary<Type, object> Option  => ModuleRegisterInfo.FinalConfigures;
}

/// <summary>
/// 泛型模块注册上下文，提供特定模块选项的访问
/// </summary>
/// <typeparam name="TModuleOption">模块选项类型</typeparam>
public class ModuleRegisterContextWrapper<TModuleOption>(ModuleRegisterContext context)  where TModuleOption : IMoModuleOption
{
    protected ModuleRegisterContext Context { get; init; } = context;
    /// <summary>
    /// 获取当前模块的设置
    /// </summary>
    public TModuleOption ModuleOption => (TModuleOption) Context.Option[typeof(TModuleOption)];
    
    /// <summary>
    /// 获取模块额外选项，如果不存在则创建新实例
    /// </summary>
    /// <typeparam name="TModuleExtraOption">模块额外选项类型</typeparam>
    /// <returns>模块额外选项实例</returns>
    public TModuleExtraOption GetModuleExtraOption<TModuleExtraOption>() where TModuleExtraOption : IMoModuleOptionBase, new()
    {
        return GetModuleExtraOptionOrDefault<TModuleExtraOption>() ?? new TModuleExtraOption();
    }
    
    /// <summary>
    /// 获取模块额外选项，如果不存在则返回默认值
    /// </summary>
    /// <typeparam name="TModuleExtraOption">模块额外选项类型</typeparam>
    /// <returns>模块额外选项实例或默认值</returns>
    public TModuleExtraOption? GetModuleExtraOptionOrDefault<TModuleExtraOption>() where TModuleExtraOption : IMoModuleOptionBase, new()
    {
        if (Context.Option.TryGetValue(typeof(TModuleExtraOption), out var option))
        {
            return (TModuleExtraOption) option;
        }

        return default;
    }
}

public class ModuleRegisterContextWrapperForServices<TModuleOption>(ModuleRegisterContext context) : ModuleRegisterContextWrapper<TModuleOption>(context) where TModuleOption : IMoModuleOption
{
    public IServiceCollection Services => Context.Services!;
}

public class ModuleRegisterContextWrapperForApplicationBuilder<TModuleOption>(ModuleRegisterContext context) : ModuleRegisterContextWrapper<TModuleOption>(context) where TModuleOption : IMoModuleOption
{
    public IApplicationBuilder ApplicationBuilder => Context.ApplicationBuilder!;
    public WebApplication WebApplication => Context.WebApplication!;
}

public class ModuleRegisterContextWrapperForBuilder<TModuleOption>(ModuleRegisterContext context) : ModuleRegisterContextWrapper<TModuleOption>(context) where TModuleOption : IMoModuleOption
{
    public WebApplicationBuilder WebApplicationBuilder => Context.WebApplicationBuilder!;
}



public class ModuleRegisterRequest(string key)
{
    public Action<ModuleRegisterContext>? ConfigureContext { get; set; }
    /// <summary>
    /// 相同Key的配置只执行一次
    /// </summary>
    public string Key { get; set; } = key;
    public EMoModules? RequestFrom { get; set; }
    public EMoModuleConfigMethods? RequestMethod { get; set; }
    public int Order { get; set; }

    public override string ToString()
    {
        return Key;
    }
}