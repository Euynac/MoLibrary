using Microsoft.Extensions.Logging;

namespace BuildingBlocksPlatform.DataChannel;

public class DataChannelSetting
{
   
    /// <summary>
    /// 日志记录器，不配置默认使用ConsoleLogger
    /// </summary>
    public ILogger? Logger { get; set; }
}