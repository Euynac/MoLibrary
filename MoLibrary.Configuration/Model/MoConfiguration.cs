using Microsoft.Extensions.Configuration;
using MoLibrary.Configuration.Annotations;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Configuration.Model;

/// <summary>
/// 配置类信息
/// </summary>
public class MoConfiguration
{
    /// <summary>
    /// 配置类类型
    /// </summary>
    public Type ConfigType { get; }

    /// <summary>
    /// 配置类名
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 配置类Key，用于修改
    /// </summary>
    public string Key => Info.Section ?? Name;
    /// <summary>
    /// 配置项信息
    /// </summary>
    public List<OptionItem> OptionItems { get; protected set; }

    /// <summary>
    /// 配置类信息
    /// </summary>
    public ConfigurationAttribute Info { get; set; }

    /// <summary>
    /// 配置版本
    /// </summary>
    public string Version { get; set; } = "";
    public static MoConfiguration Create<T>(T config)
    {
        return new MoConfiguration(typeof(T), config);
    }

    public MoConfiguration(Type configType) : this(configType, null)
    {
        
    }
    public MoConfiguration(Type configType, object? configInstance)
    {
        ConfigType = configType;
        Name = configType.Name;
        Info = UtilsConfiguration.GetConfigAttribute(ConfigType)!;
        OptionItems = CreateOptionItems(configInstance);
    }

    private List<OptionItem> CreateOptionItems(object? configInstance)
    {
        return OptionItem.CreateItems(ConfigType, configInstance, Info.Section);
    }
    public void SetOptionSource(IConfigurationProvider provider, string? sourceInfo)
    {
        foreach (var item in OptionItems)
        {
            item.SetSource(provider, sourceInfo);
        }
    }
    public void SetOptionValue(object? configInstance)
    {
        foreach (var item in OptionItems)
        {
            item.SetValueFromConfigInstance(configInstance);
        }
    }

    public override string ToString()
    {
        return OptionItems.Where(x => MoConfigurationManager.Setting.EnableLoggingWithoutOptionSetting || x.Info != null)
                .Select(p => $"[{Name}] {p}").StringJoin("\n")
                .BeNullIfWhiteSpace() ?? $"[{Name}] Can not get any options info";
    }
}