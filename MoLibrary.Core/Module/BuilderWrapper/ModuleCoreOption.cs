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
    public static string? DefaultModuleSwaggerGroupName { get; set; } 

    /// <summary>
    /// 设置含有Endpoints的模块的默认Swagger分组名称
    /// </summary>
    /// <param name="swaggerGroupName">Swagger分组名称</param>
    public void SetDefaultModuleSwaggerGroupName(string swaggerGroupName)
    {
        DefaultModuleSwaggerGroupName = swaggerGroupName;
    }
}