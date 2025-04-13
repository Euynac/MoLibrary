using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

/// <summary>
/// MoLibrary模块接口
/// 定义模块的配置和初始化方法
/// </summary>
public interface IMoLibraryModule
{
    /// <summary>
    /// 配置WebApplicationBuilder
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例</param>
    void ConfigureBuilder(WebApplicationBuilder builder);

    /// <summary>
    /// 配置服务依赖注入
    /// </summary>
    /// <param name="services">服务集合</param>
    void ConfigureServices(IServiceCollection services);
    
    /// <summary>
    /// 使用中间件
    /// </summary>
    /// <param name="application">应用程序构建器</param>
    void UseMiddlewares(IApplicationBuilder application);
}

/// <summary>
/// MoLibrary模块抽象基类
/// 提供IMoLibraryModule接口的默认实现
/// </summary>
public abstract class MoLibraryModule : IMoLibraryModule
{
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
}