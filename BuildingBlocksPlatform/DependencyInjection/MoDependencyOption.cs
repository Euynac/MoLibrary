using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocksPlatform.DependencyInjection;

public class MoDependencyOption
{
    /// <summary>
    /// 相关项目单元所在程序集名，使用名称包含查找。如若不配置，则默认仅扫描Entry程序集。
    /// </summary>
    public string[]? RelatedAssemblies { get; set; }
    public ILogger Logger { get; set; } = NullLogger.Instance;
    public bool EnableDebug { get; set; }
}

