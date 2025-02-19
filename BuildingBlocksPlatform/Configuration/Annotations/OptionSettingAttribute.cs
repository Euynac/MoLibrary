namespace BuildingBlocksPlatform.Configuration.Annotations;

[AttributeUsage(AttributeTargets.Property)]
public class OptionSettingAttribute : Attribute
{
    public OptionSettingAttribute()
    {
        
    }

    public OptionSettingAttribute(string title)
    {
        Title = title;
    }

    /// <summary>
    /// 日志记录格式，使用{0}表示值
    /// </summary>
    public string? LoggingFormat { get; set; }

    /// <summary>
    /// 配置项标题，将显示到Dashboard
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 配置描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 标记为离线参数，指示重启才能生效
    /// </summary>
    public bool IsOffline
    {
        get => _IsOffline ?? false;
        set => _IsOffline = value;
    }

    /// <summary>
    /// 标记为离线参数，指示重启才能生效
    /// </summary>
    internal bool? _IsOffline { get; set; }
}