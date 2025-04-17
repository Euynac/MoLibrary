using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace MoLibrary.Core.Module;

/// <summary>
/// MoLibrary模块接口
/// 定义模块的配置和初始化方法
/// </summary>
public interface IMoModule
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


    EMoModules GetMoModuleEnum();
}