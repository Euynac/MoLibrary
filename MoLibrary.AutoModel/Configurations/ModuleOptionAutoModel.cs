using MoLibrary.Core.Module.Interfaces;

namespace MoLibrary.AutoModel.Configurations;

public class ModuleOptionAutoModel : IMoModuleOption<ModuleAutoModel>
{
    /// <summary>
    /// 全局主动模式（仅使用了AutoField标签的字段才会启用自动模型功能）
    /// </summary>
    public bool EnableActiveMode { get; set; }

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

    public bool DisableAutoIgnorePropertyWithJsonIgnoreAttribute { get; set; }
    public bool DisableAutoIgnorePropertyWithNotMappedAttribute { get; set; }

    /// <summary>
    /// 开启对于不支持的字段类型进行异常报错
    /// </summary>
    public bool EnableErrorForUnsupportedFieldTypes { get; set; }
}