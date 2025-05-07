using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Configuration.Providers;
using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.Configuration.Modules;

public class ModuleConfigurationOption : IMoModuleOption<ModuleConfiguration>, IMoModuleControllerOption<ModuleConfiguration>
{

    /// <summary>
    /// When false (the default), no exceptions are thrown when a configuration key is found for which the
    /// provided model object does not have an appropriate property which matches the key's name.
    /// When true, an <see cref="InvalidOperationException"/> is thrown with a description
    /// of the missing properties.
    /// </summary>
    /// <remarks>
    /// 这用于检查是否给定Dictionary中的所有键都有指定配置类匹配的属性。所以不会用于HostConfiguration的配置，而是针对指定配置源映射指定配置类。
    /// </remarks>
    public bool ErrorOnUnknownConfiguration { get; set; }

    /// <summary>
    /// 如果配置类没有被<see cref="ConfigurationAttribute"/>标记，则抛出异常。默认不抛出异常，仅记录日志。
    /// </summary>
    public bool ErrorOnNoTagConfigAttribute { get; set; }
    /// <summary>
    /// 启用当使用<see cref="ConfigurationAttribute"/>时，其配置参数必须同时使用<see cref="OptionSettingAttribute"/>，否则抛出异常
    /// </summary>
    public bool ErrorOnNoTagOptionAttribute { get; set; }
    /// <summary>
    /// 增加Dapr Configuration作为配置Provider
    /// </summary>
    internal bool UseDaprProvider => DaprStoreName != null;

    /// <summary>
    /// Dapr Configuration Store Name，用于Dapr Configuration Provider
    /// </summary>
    public string? DaprStoreName { get; set; }

    /// <summary>
    /// 开启配置读取日志
    /// TODO 暂未实现，拟通过动态注入set方法实现
    /// </summary>
    public bool EnableReadConfigLogging { get; set; }

    /// <summary>
    /// 开启配置注册日志
    /// </summary>
    public bool EnableConfigRegisterLogging { get; set; }

    /// <summary>
    /// 日志记录器，不配置默认使用ConsoleLogger
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// 应用程序相关配置字典实例
    /// </summary>
    public IConfiguration AppConfiguration { get; set; } = null!;
    /// <summary>
    /// 配置类所在程序集名，使用名称包含查找。如若不配置，则默认仅扫描Entry程序集。
    /// </summary>
    public string[]? ConfigurationAssemblyLocation { get; set; }

    /// <summary>
    /// 是否允许在没有配置项特性的情况下对选项进行日志记录
    /// </summary>
    public bool EnableLoggingWithoutOptionSetting { get; set; }
 

    /// <summary>
    /// 设定当前微服务是配置中心
    /// </summary>
    public bool ThisIsDashboard { get; set; } = false;

    #region 配置文件管理

    /// <summary>
    /// 按照配置类生成配置文件进行管理（生成在程序运行路径下）
    /// </summary>
    public bool GenerateFileForEachOption { get; set; }

    /// <summary>
    /// 配置类生成配置文件的父级文件夹
    /// </summary>
    public string? GenerateOptionFileParentDirectory { get; set; } = "configs";
    
    /// <summary>
    /// 指定如何处理配置类中删除的属性
    /// </summary>
    public LocalJsonFileProvider.RemovedPropertyHandling RemovedPropertyHandling { get; set; } = LocalJsonFileProvider.RemovedPropertyHandling.Comment;
    #endregion


    /// <summary>
    /// 设置其他配置来源，优先级高。（优先级就是读取的顺序，后面的读取重复的会覆盖前面的配置）
    /// 默认读取规则：
    /// <para></para>JsonDocumentOptions options = new JsonDocumentOptions()
    /// <para></para>{
    /// <para></para>  CommentHandling = JsonCommentHandling.Skip,
    /// <para></para>  AllowTrailingCommas = true
    /// <para></para>};
    /// </summary>
    public Action<ConfigurationManager>? SetOtherSourceAction { get; set; }

    public string? SwaggerGroupName { get; set; }
}