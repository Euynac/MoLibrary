using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MoLibrary.Core.Module.Models;
using MoLibrary.Tool.MoResponse;

namespace MoLibrary.Core.Module.Interfaces;

/// <summary>
/// MoLibrary模块接口
/// 定义模块的配置和初始化方法
/// 当任何一个模块的配置阶段返回错误时，该模块及其依赖项的配置将被中止
/// </summary>
public interface IMoModule
{
    /// <summary>
    /// 配置WebApplicationBuilder
    /// </summary>
    /// <param name="builder">WebApplicationBuilder实例</param>
    Res ConfigureBuilder(WebApplicationBuilder builder);

    /// <summary>
    /// 配置服务依赖注入
    /// </summary>
    /// <param name="services">服务集合</param>
    Res ConfigureServices(IServiceCollection services);

    /// <summary>
    /// 在执行遍历业务程序集类<see cref="IWantIterateBusinessTypes"/>后配置服务依赖注入
    /// </summary>
    /// <param name="services">服务集合</param>
    Res PostConfigureServices(IServiceCollection services);

    /// <summary>
    /// 配置应用程序管道
    /// </summary>
    /// <param name="app"></param>
    Res ConfigureApplicationBuilder(IApplicationBuilder app);

    EMoModules CurModuleEnum();
}