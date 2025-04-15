using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

public class ModuleExtraActionInfo
{
    public Action<IServiceCollection>? ConfigureServices { get; set; }
    public string? Key { get; set; }
}

/// <summary>
/// MoLibrary模块抽象基类
/// 提供IMoLibraryModule接口的默认实现
/// </summary>
public abstract class MoModule : IMoLibraryModule
{
    public static Dictionary<Type, List<ModuleExtraActionInfo>> ModuleExtraActions { get; set; } = new();

    /// <summary>
    /// 配置WebApplicationBuilder
    /// 默认实现为空，子类可根据需要重写
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例</param>
    public virtual void ConfigureBuilder(WebApplicationBuilder builder)
    {
    }

    /// <summary>
    /// 配置服务依赖注入
    /// 默认实现为空，子类可根据需要重写
    /// </summary>
    /// <param name="services">服务集合</param>
    public virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// 使用中间件
    /// 默认实现为空，子类可根据需要重写
    /// </summary>
    /// <param name="application">应用程序构建器</param>
    public virtual void UseMiddlewares(IApplicationBuilder application)
    {
    }


    public static void ConfigureExtraServices<TModule>(Action<IServiceCollection> configureServices) where TModule : MoModule
    {
        var moduleType = typeof(TModule);
        if (!ModuleExtraActions.TryGetValue(moduleType, out var extraActions))
        {
            extraActions = new List<ModuleExtraActionInfo>();
            ModuleExtraActions[moduleType] = extraActions;
        }
        extraActions.Add(new ModuleExtraActionInfo { ConfigureServices = configureServices });
    }

    public static void ConfigureExtraServicesOnce<TModule>(string key, Action<IServiceCollection> configureServices) where TModule : MoModule
    {
        var moduleType = typeof(TModule);
        if (!ModuleExtraActions.TryGetValue(moduleType, out var extraActions))
        {
            extraActions = new List<ModuleExtraActionInfo>();
            ModuleExtraActions[moduleType] = extraActions;
        }
        if (extraActions.Any(x => x.Key == key))
        {
            return;
            //throw new InvalidOperationException($"ConfigureExtraServicesOnce for {moduleType} with key {key} already exists");
        }
        extraActions.Add(new ModuleExtraActionInfo { ConfigureServices = configureServices, Key = key });
    }
}