using Microsoft.Extensions.Logging;
using MoLibrary.Core.ModuleController;

namespace MoLibrary.DataChannel;

public class DataChannelSetting : IMoModuleOptions
{
   
    /// <summary>
    /// 日志记录器，不配置默认使用ConsoleLogger
    /// </summary>
    public ILogger? Logger { get; set; }

    public string RoutePrefix { get; set; } = "DataChannel";
    public string SwaggerTag { get; set; } = "DataChannel";
    public bool EnableControllers { get; set; }
}