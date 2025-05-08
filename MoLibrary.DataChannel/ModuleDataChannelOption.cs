using Microsoft.Extensions.Logging;
using MoLibrary.Core.Module.Interfaces;
using MoLibrary.DataChannel.Modules;

namespace MoLibrary.DataChannel;

/// <summary>
/// 数据通道配置类
/// 用于配置数据通道的全局设置和选项
/// 实现了IMoModuleOptions接口，支持模块化配置
/// </summary>
public class ModuleDataChannelOption : MoModuleControllerOption<ModuleDataChannel>
{
    /// <summary>
    /// 日志记录器实例
    /// 如果不配置，则默认使用ConsoleLogger
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// API路由前缀
    /// 用于配置DataChannel相关控制器的路由前缀
    /// </summary>
    public string RoutePrefix { get; set; } = "DataChannel";
    
    /// <summary>
    /// Swagger文档标签
    /// 用于在Swagger UI中对DataChannel相关API进行分组
    /// </summary>
    public string SwaggerTag { get; set; } = "DataChannel";
    
    /// <summary>
    /// 是否启用控制器
    /// 控制是否注册和启用DataChannel相关的API控制器
    /// </summary>
    public bool EnableControllers { get; set; }
}