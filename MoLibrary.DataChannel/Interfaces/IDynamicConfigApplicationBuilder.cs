using Microsoft.AspNetCore.Builder;

namespace MoLibrary.DataChannel.Interfaces;

/// <summary>
/// 动态应用配置接口
/// 允许组件在ASP.NET Core注册阶段自动配置应用程序
/// 适用于需要动态配置ASP.NET Core管道的中间件和端点
/// </summary>
public interface IDynamicConfigApplicationBuilder
{
    /// <summary>
    /// 执行应用程序配置
    /// 通过组件实例配置应用程序构建器
    /// 注意：同种类型的组件可能会被多次调用此方法
    /// </summary>
    /// <param name="app">应用程序构建器实例</param>
    public void DoConfigApplication(IApplicationBuilder app);
}