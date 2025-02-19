namespace BuildingBlocksPlatform.AutoModel.Configurations;

/// <summary>
/// 字段模糊查询设置
/// </summary>
public class AutoModelFuzzSetting
{
    /// <summary>
    /// 目前不支持的类型
    /// </summary>
    public bool IsNotSupported { get; set; }
    /// <summary>
    /// 全字段模糊查询时是否忽略该字段
    /// </summary>
    public bool IsIgnored { get; set; }
}