namespace Monica.Configuration.UI.Model;

/// <summary>
/// 配置来源分析结果
/// </summary>
public class ConfigSourceAnalysis
{
    /// <summary>
    /// 配置类名
    /// </summary>
    public string ConfigName { get; set; } = string.Empty;

    /// <summary>
    /// 配置类标题
    /// </summary>
    public string ConfigTitle { get; set; } = string.Empty;

    /// <summary>
    /// 配置项来源是否一致
    /// </summary>
    public bool IsConsistent { get; set; }

    /// <summary>
    /// 按来源分组的配置项
    /// </summary>
    public List<ConfigSourceGroup> SourceGroups { get; set; } = new();
}

/// <summary>
/// 配置来源分组
/// </summary>
public class ConfigSourceGroup
{
    /// <summary>
    /// Provider类型
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// 来源信息
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// 该来源下的配置项
    /// </summary>
    public List<ConfigItemSourceInfo> Items { get; set; } = new();
}

/// <summary>
/// 配置项来源信息
/// </summary>
public class ConfigItemSourceInfo
{
    /// <summary>
    /// 配置项Key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 配置项标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 配置项名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
}
