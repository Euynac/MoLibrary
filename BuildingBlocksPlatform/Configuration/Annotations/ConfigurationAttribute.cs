namespace BuildingBlocksPlatform.Configuration.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public class ConfigurationAttribute : Attribute
{
    /// <summary>
    /// 自定义配置节点名称。用于Json类配置读取。Json节点名称。如果为空则默认使用类名作为节点名称。
    /// </summary>
    public string? Section { get; internal set; }

    /// <summary>
    /// 不使用配置节点名称，指示该配置类中配置项是孤立的Key-Value型配置 TODO 目前文件型不支持修改孤立节点型配置
    /// </summary>
    public bool DisableSection { get; set; }

    /// <summary>
    /// 是否不需要在Dashboard中显示以配置【未实现】
    /// <para>Dashboard：一级菜单：子域；二级菜单：子域下微服务；三级菜单：微服务下配置类；版本控制到微服务配置</para>
    /// </summary>
    public bool HideFromDashboard { get; set; }

    /// <summary>
    /// Dashboard中显示的配置类名
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 配置类别，通过自定义const配置类别限制，用于Dashboard分组
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// 配置类描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否是子配置（依赖上层配置，不会单独存在）
    /// </summary>
    public bool IsSubConfiguration { get; set; }

    /// <summary>
    /// 指示旗下配置项是离线参数，重启才能生效
    /// </summary>
    public bool IsOffline
    {
        get => _IsOffline ?? false;
        set => _IsOffline = value;
    }

    internal bool? _IsOffline { get; set; }
    /// <summary>
    /// 该配置以类名作为配置节点名称
    /// </summary>
    public ConfigurationAttribute()
    {
    }

    /// <summary>
    /// 指定该配置类是有配置节点名称的
    /// </summary>
    /// <param name="section"></param>
    public ConfigurationAttribute(string section)
    {
        Section = section;
    }

    //以下是官方OptionBinder的配置
    /// <summary>
    /// When false (the default), the binder will only attempt to set public properties.
    /// If true, the binder will attempt to set all non read-only properties.
    /// </summary>
    public bool? BindNonPublicProperties { get; set; }

    /// <summary>
    /// When false (the default), no exceptions are thrown when a configuration key is found for which the
    /// provided model object does not have an appropriate property which matches the key's name.
    /// When true, an <see cref="System.InvalidOperationException"/> is thrown with a description
    /// of the missing properties.
    /// </summary>
    public bool? ErrorOnUnknownConfiguration { get; set; }
}