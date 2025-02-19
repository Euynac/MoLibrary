using BuildingBlocksPlatform.AutoModel.Annotations;

namespace BuildingBlocksPlatform.AutoModel.Configurations;

public class AutoModelOptions
{
    /// <summary>
    /// 全局主动模式（仅使用了AutoField标签的字段才会启用自动模型功能）
    /// </summary>
    public bool ActiveMode { get; set; }

    /// <summary>
    /// 默认激活名开启前缀忽略
    /// </summary>
    public bool EnableIgnorePrefix { get; set; }

    /// <summary>
    /// 默认激活名开启前缀忽略后，自动调整失败的激活名不报错
    /// </summary>
    public bool EnableIgnorePrefixAutoAdjust { get; set; }

    /// <summary>
    /// 开启调试模式（如显示Filter实际生成Expression）
    /// </summary>
    public bool EnableDebugging { get; set; }
    /// <summary>
    /// 开启将字段显示名作为激活名
    /// </summary>
    [Obsolete("暂未实现")]
    public bool EnableTitleAsActivateName { get; set; }
}