using Microsoft.Extensions.Logging;

namespace MoLibrary.Core.Module.BuilderWrapper;

public class ModuleCoreOption
{
    /// <summary>
    /// 启用立即注册特性。注意对于有嵌套依赖的模块注册慎用，会使得后续的这些模块配置失效，因为已经被注册。
    /// </summary>
    public bool EnableRegisterInstantly { get; set; }

    /// <summary>
    /// 模块默认日志级别
    /// </summary>
    public static LogLevel DefaultModuleLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// 如果模块注册出现异常则禁用Module，而不是抛出异常。
    /// 当设置为 true 时，如果模块在注册过程中出现异常，系统将记录错误并禁用该模块，而不是抛出异常中断整个应用程序的启动。
    /// 被禁用的模块在应用程序的生命周期内将被完全跳过，不会调用其任何配置或初始化方法。
    /// </summary>
    public static bool DisableModuleIfHasException { get; set; }

    /// <summary>
    /// 启用模块系统初始化后输出模块执行状态报告日志
    /// </summary>
    public static bool EnableLoggingModuleSummary { get; set; }

    /// <summary>
    /// 设置模块默认日志级别
    /// </summary>
    /// <param name="logLevel">日志级别</param>
    public void SetDefaultModuleLogLevel(LogLevel logLevel)
    {
        DefaultModuleLogLevel = logLevel;
    }


    /// <summary>
    /// 设置含有Endpoints的模块的默认Swagger分组名称，默认情况下以模块名为分组名称。
    /// </summary>
    public static string? DefaultModuleApiGroupName { get; set; } 

    /// <summary>
    /// 设置含有Endpoints的模块的默认Swagger分组名称
    /// </summary>
    /// <param name="swaggerGroupName">Swagger分组名称</param>
    public void SetDefaultModuleSwaggerGroupName(string swaggerGroupName)
    {
        DefaultModuleApiGroupName = swaggerGroupName;
    }

    /// <summary>
    /// 设置模块的默认路由前缀，默认情况下以模块名为路由前缀。
    /// </summary>
    public static string? DefaultRoutePrefix { get; set; }

    /// <summary>
    /// 设置模块的默认路由前缀
    /// </summary>
    /// <param name="routePrefix">路由前缀</param>
    public void SetDefaultRoutePrefix(string routePrefix)
    {
        DefaultRoutePrefix = routePrefix;
    }

    /// <summary>
    /// 设置模块的默认控制器禁用状态，默认情况下不禁用控制器。
    /// </summary>
    public static bool? DefaultDisableControllers { get; set; }

    /// <summary>
    /// 设置模块的默认控制器禁用状态
    /// </summary>
    /// <param name="disableControllers">是否禁用控制器</param>
    public void SetDefaultDisableControllers(bool disableControllers)
    {
        DefaultDisableControllers = disableControllers;
    }

    /// <summary>
    /// 设置模块的默认Swagger可见性，默认情况下在Swagger中可见。
    /// </summary>
    public static bool? DefaultIsVisibleInSwagger { get; set; }

    /// <summary>
    /// 设置模块的默认Swagger可见性
    /// </summary>
    /// <param name="isVisibleInSwagger">是否在Swagger中可见</param>
    public void SetDefaultIsVisibleInSwagger(bool isVisibleInSwagger)
    {
        DefaultIsVisibleInSwagger = isVisibleInSwagger;
    }
}