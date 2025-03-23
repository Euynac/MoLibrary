using Microsoft.AspNetCore.Builder;

namespace MoLibrary.DataChannel.Interfaces;

/// <summary>
/// ASP.NET Core注册阶段自动执行（Middleware与Endpoints均支持）
/// </summary>
public interface IDynamicConfigApplicationBuilder
{
    /// <summary>
    /// 进行应用配置。通过单例实例配置，同种Middleware或Endpoints会配置多次。
    /// </summary>
    /// <param name="app"></param>
    public void DoConfigApplication(IApplicationBuilder app)
    {

    }
}
///// <summary>
///// ASP.NET Core注册阶段自动执行（Middleware与Endpoints均支持）
///// </summary>
//public interface INeedConfigAspNetCore
//{
//    /// <summary>
//    /// 进行服务注册
//    /// </summary>
//    /// <param name="services"></param>
//    public static abstract void DoRegister(IServiceCollection services);

//    /// <summary>
//    /// 进行应用配置
//    /// </summary>
//    /// <param name="app"></param>
//    public static abstract void DoConfigApplication(IApplicationBuilder app);
//}